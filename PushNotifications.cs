using System.Collections.Concurrent;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Channels;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public class PushApplicationKey
{
    public int Id { get; set; }
    public string PublicKey { get; set; } = "";
    public string PrivateKey { get; set; } = "";
}
public class PushDevice
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public AppUser User { get; set; } = null!;
    public string SecurityStamp { get; set; } = "";
    public string Endpoint { get; set; } = "";
    public string EndpointHash { get; set; } = "";
    public string P256dh { get; set; } = "";
    public string Auth { get; set; } = "";
    public DateTime ExpiresAt { get; set; }
    public DateTime? LastAcceptedAt { get; set; }
    public string? LastError { get; set; }
}
public class PushDelivery
{
    public long Id { get; set; }
    public Guid DeviceId { get; set; }
    public PushDevice Device { get; set; } = null!;
    public string RecipientId { get; set; } = "";
    public long? NotificationId { get; set; }
    public Notification? Notification { get; set; }
    public int Attempts { get; set; }
    public DateTime AvailableAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public sealed class PushSignal
{
    readonly Channel<bool> channel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropOldest });
    public void Notify() => channel.Writer.TryWrite(true);
    public async Task Wait(TimeSpan interval, CancellationToken stoppingToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        timeout.CancelAfter(interval);
        try { await channel.Reader.ReadAsync(timeout.Token); }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested) { }
    }
}

public sealed class PushIdentity(IConfiguration config)
{
    string publicKey = "", privateKey = "";
    readonly ConcurrentDictionary<string, (string Header, DateTimeOffset Refresh)> tokens = new();
    public bool Enabled => config.GetValue("Push:Enabled", true);
    public string PublicKey => publicKey;
    public async Task Initialize(AppDb db)
    {
        if (!Enabled) return;
        var existing = await db.PushApplicationKeys.AsNoTracking().SingleOrDefaultAsync(x => x.Id == 1);
        if (existing == null)
        {
            var pair = WebPushProtocol.GenerateKeys();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "PushApplicationKeys" ("Id", "PublicKey", "PrivateKey") VALUES (1, {pair.PublicKey}, {pair.PrivateKey})
                ON CONFLICT ("Id") DO NOTHING
                """);
            existing = await db.PushApplicationKeys.AsNoTracking().SingleAsync(x => x.Id == 1);
        }
        publicKey = existing.PublicKey;
        privateKey = existing.PrivateKey;
    }
    public string Authorization(string endpoint)
    {
        var origin = new Uri(endpoint).GetLeftPart(UriPartial.Authority);
        var now = DateTimeOffset.UtcNow;
        if (tokens.TryGetValue(origin, out var cached) && cached.Refresh > now) return cached.Header;
        var publicUrl = config["PublicUrl"] ?? "";
        var subject = config["Push:Subject"] ?? (publicUrl.StartsWith("https://", StringComparison.Ordinal) ? publicUrl : "mailto:push@localhost");
        var header = WebPushProtocol.Authorization(endpoint, publicKey, privateKey, subject, now);
        tokens[origin] = (header, now.AddHours(1));
        return header;
    }
}

public record PushSubscriptionInput(string Endpoint, string P256dh, string Auth);
public static class PushEndpoints
{
    public static void MapPush(this WebApplication app)
    {
        var endpoints = app.MapGroup("/api/push").RequireAuthorization().RequireRateLimiting("auth");
        endpoints.MapGet("/config", (PushIdentity identity) => Results.Ok(new { enabled = identity.Enabled, publicKey = identity.PublicKey }));
        endpoints.MapGet("/devices/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDb db) =>
        {
            var device = await db.PushDevices.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.UserId == principal.FindFirstValue(ClaimTypes.NameIdentifier));
            return Results.Ok(new { enabled = device != null && device.ExpiresAt > DateTime.UtcNow,
                expiresAt = device?.ExpiresAt, lastAcceptedAt = device?.LastAcceptedAt, lastError = device?.LastError });
        });
        endpoints.MapPut("/devices/{id:guid}", async (Guid id, PushSubscriptionInput input, HttpContext context, AppDb db, PushIdentity identity, PushSignal signal) =>
        {
            if (!identity.Enabled) throw new RuleException(503, "push_disabled", "Известията за устройство са временно недостъпни.");
            var testOrigin = app.Environment.IsEnvironment("Testing") ? app.Configuration["Push:TestOrigin"] : null;
            if (id == Guid.Empty || input.Endpoint == null || !WebPushProtocol.AllowedEndpoint(input.Endpoint, testOrigin) || !WebPushProtocol.ValidSubscriptionKeys(input.P256dh, input.Auth))
                throw new RuleException(400, "invalid_push", "Невалиден адрес за известия на устройството.");
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var user = await db.Users.SingleAsync(x => x.Id == userId);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.Endpoint)));
            var device = await db.PushDevices.SingleOrDefaultAsync(x => x.Id == id);
            if (device == null && await db.PushDevices.CountAsync(x => x.UserId == userId) >= 10)
                throw new RuleException(400, "push_limit", "Достигнат е лимитът от 10 устройства за профила.");
            if (await db.PushDevices.AnyAsync(x => x.EndpointHash == hash && x.Id != id)
                || (device != null && device.UserId != userId))
                throw new RuleException(409, "push_conflict", "Изключете и включете отново известията на това устройство.");
            if (device == null)
            {
                device = new PushDevice { Id = id };
                db.PushDevices.Add(device);
            }
            var authentication = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);
            if (device.EndpointHash != hash || device.P256dh != input.P256dh || device.Auth != input.Auth) { device.LastAcceptedAt = null; device.LastError = null; }
            device.UserId = userId;
            device.SecurityStamp = user.SecurityStamp ?? "";
            device.Endpoint = input.Endpoint;
            device.EndpointHash = hash;
            device.P256dh = input.P256dh;
            device.Auth = input.Auth;
            device.ExpiresAt = authentication.Properties?.ExpiresUtc?.UtcDateTime ?? DateTime.UtcNow.AddDays(14);
            await db.SaveChangesAsync();
            signal.Notify();
            return Results.NoContent();
        });
        endpoints.MapDelete("/devices/{id:guid}", async (Guid id, ClaimsPrincipal principal, AppDb db) =>
        {
            var removed = await db.PushDevices.Where(x => x.Id == id && x.UserId == principal.FindFirstValue(ClaimTypes.NameIdentifier)).ExecuteDeleteAsync();
            return removed > 0 ? Results.NoContent() : Results.NotFound();
        });
        endpoints.MapPost("/devices/{id:guid}/test", async (Guid id, ClaimsPrincipal principal, AppDb db, PushIdentity identity, PushSignal signal) =>
        {
            if (!identity.Enabled) return Results.StatusCode(503);
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!;
            if (!await db.PushDevices.AnyAsync(x => x.Id == id && x.UserId == userId && x.ExpiresAt > DateTime.UtcNow))
                return Results.NotFound();
            db.PushDeliveries.Add(new PushDelivery { DeviceId = id, RecipientId = userId });
            await db.SaveChangesAsync();
            signal.Notify();
            return Results.Accepted();
        });
    }
}

public sealed class PushWorker(IServiceScopeFactory scopes, PushIdentity identity, PushSignal signal,
    IHttpClientFactory clients, IConfiguration config, ILogger<PushWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!identity.Enabled) return;
        var interval = TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Push:RetryIntervalSeconds", 900), 1, 3600));
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Dispatch(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogWarning("Push dispatch postponed ({ErrorType})", ex.GetType().Name); }
            try { await signal.Wait(interval, stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }
    async Task Dispatch(CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var lockDb = scope.ServiceProvider.GetRequiredService<AppDb>();
        await lockDb.Database.OpenConnectionAsync(token);
        try
        {
            await using var acquire = lockDb.Database.GetDbConnection().CreateCommand();
            acquire.CommandText = "SELECT pg_try_advisory_lock(735091246)";
            if (!Equals(await acquire.ExecuteScalarAsync(token), true)) return;
            try
            {
                await lockDb.PushDevices.Where(x => x.ExpiresAt <= DateTime.UtcNow || x.User.Disabled || x.SecurityStamp != x.User.SecurityStamp).ExecuteDeleteAsync(token);
                await lockDb.PushDeliveries.Where(x => x.CreatedAt < DateTime.UtcNow.AddDays(-1) || x.Attempts >= 5).ExecuteDeleteAsync(token);
                var ids = await lockDb.PushDeliveries.Where(x => x.AvailableAt <= DateTime.UtcNow).OrderBy(x => x.Id).Select(x => x.Id).Take(100).ToListAsync(token);
                foreach (var id in ids) await Deliver(id, token);
                if (ids.Count == 100) signal.Notify();
            }
            finally
            {
                await using var release = lockDb.Database.GetDbConnection().CreateCommand();
                release.CommandText = "SELECT pg_advisory_unlock(735091246)";
                await release.ExecuteScalarAsync(CancellationToken.None);
            }
        }
        finally { await lockDb.Database.CloseConnectionAsync(); }
    }
    async Task Deliver(long id, CancellationToken token)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        var delivery = await db.PushDeliveries.Include(x => x.Device).ThenInclude(x => x.User).Include(x => x.Notification).SingleOrDefaultAsync(x => x.Id == id, token);
        if (delivery == null) return;
        var device = delivery.Device;
        var permitted = device.UserId == delivery.RecipientId && !device.User.Disabled
            && device.SecurityStamp == device.User.SecurityStamp && device.ExpiresAt > DateTime.UtcNow;
        if (permitted && delivery.Notification is { } notice)
            permitted = await db.Members.AnyAsync(x => x.UserId == delivery.RecipientId && x.GroupId == notice.GroupId && !x.Group.Disabled, token);
        if (!permitted)
        {
            db.PushDeliveries.Remove(delivery);
            await db.SaveChangesAsync(token);
            return;
        }
        try
        {
            if (!WebPushProtocol.ValidSubscriptionKeys(device.P256dh, device.Auth))
            {
                device.LastError = "SubscriptionNeedsRefresh";
                Postpone(delivery);
                await db.SaveChangesAsync(token);
                return;
            }
            var at = delivery.Notification?.At ?? DateTime.UtcNow;
            var body = delivery.Notification?.Text ?? $"Тестово известие · {NotificationText.Time(at)}";
            var payload = NotificationText.Payload(body, at, delivery.Notification?.Title ?? "Тестово известие");
            var encrypted = WebPushProtocol.Encrypt(payload, device.P256dh, device.Auth);
            using var request = WebPushProtocol.Request(device.Endpoint, identity.Authorization(device.Endpoint), encrypted);
            using var response = await clients.CreateClient("PushTransport").SendAsync(request, HttpCompletionOption.ResponseHeadersRead, token);
            if (response.StatusCode is HttpStatusCode.Gone or HttpStatusCode.NotFound)
                db.PushDevices.Remove(device);
            else if (response.IsSuccessStatusCode)
            {
                device.LastAcceptedAt = DateTime.UtcNow;
                device.LastError = null;
                db.PushDeliveries.Remove(delivery);
            }
            else
            {
                device.LastError = $"HTTP {(int)response.StatusCode}";
                var retryAfter = response.Headers.RetryAfter?.Delta
                    ?? (response.Headers.RetryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : (TimeSpan?)null);
                Postpone(delivery, retryAfter);
            }
        }
        catch (Exception ex) when (!token.IsCancellationRequested)
        {
            device.LastError = ex.GetType().Name;
            Postpone(delivery);
        }
        await db.SaveChangesAsync(token);
    }
    static void Postpone(PushDelivery delivery, TimeSpan? retryAfter = null)
    {
        delivery.Attempts++;
        var seconds = Math.Min(3600, 15 * Math.Pow(2, delivery.Attempts - 1));
        if (retryAfter is { TotalSeconds: > 0 }) seconds = Math.Max(seconds, Math.Min(86400, retryAfter.Value.TotalSeconds));
        delivery.AvailableAt = DateTime.UtcNow.AddSeconds(seconds);
    }
}
