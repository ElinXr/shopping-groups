using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Api;
using ShoppingGroups.Domain;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsProduction())
{
    if (!Uri.TryCreate(builder.Configuration["PublicUrl"], UriKind.Absolute, out var publicUrl) || publicUrl.Scheme != "https" || publicUrl.PathAndQuery != "/" || publicUrl.Fragment.Length != 0 || publicUrl.UserInfo.Length != 0)
        throw new InvalidOperationException("Production PublicUrl must be an HTTPS origin.");
    if ((builder.Configuration.GetConnectionString("Database") ?? "").Contains("CHANGE_ME", StringComparison.Ordinal)
        || (builder.Configuration["Bootstrap:AdminPassword"] ?? "").Contains("CHANGE_ME", StringComparison.Ordinal))
        throw new InvalidOperationException("Replace example credentials before production startup.");
}
builder.Services.AddDbContext<AppDb>(o => o.UseNpgsql(builder.Configuration.GetConnectionString("Database")
    ?? throw new InvalidOperationException("ConnectionStrings__Database is required.")));
builder.Services.AddIdentity<AppUser, IdentityRole>(o =>
{
    o.User.RequireUniqueEmail = true;
    o.Password.RequiredLength = 10;
    o.Lockout.MaxFailedAccessAttempts = 5;
    o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
}).AddEntityFrameworkStores<AppDb>().AddDefaultTokenProviders();
builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.Zero);
builder.Services.Configure<DataProtectionTokenProviderOptions>(o => o.TokenLifespan = TimeSpan.FromHours(24));
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.Name = "shopping.session";
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    o.ExpireTimeSpan = TimeSpan.FromDays(14);
    o.SlidingExpiration = true;
    o.Events.OnRedirectToLogin = c => { c.Response.StatusCode = 401; return Task.CompletedTask; };
    o.Events.OnRedirectToAccessDenied = c => { c.Response.StatusCode = 403; return Task.CompletedTask; };
    o.Events.OnValidatePrincipal = async c =>
    {
        await SecurityStampValidator.ValidatePrincipalAsync(c);
        if (c.Principal?.Identity?.IsAuthenticated != true) return;
        var users = c.HttpContext.RequestServices.GetRequiredService<UserManager<AppUser>>();
        var user = await users.GetUserAsync(c.Principal);
        if (user == null || user.Disabled) c.RejectPrincipal();
    };
});
var protection = builder.Services.AddDataProtection().SetApplicationName("ShoppingGroupsV1");
var keys = builder.Configuration["DataProtection:Path"];
if (!string.IsNullOrEmpty(keys)) protection.PersistKeysToFileSystem(new DirectoryInfo(keys));
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(o => {
    o.HeaderName = "X-CSRF-TOKEN"; o.Cookie.Name = "shopping.csrf"; o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = builder.Environment.IsDevelopment() || builder.Environment.IsEnvironment("Testing") ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
});
builder.Services.AddSignalR(o => { o.EnableDetailedErrors = false; o.MaximumReceiveMessageSize = 4096; });
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<Collaboration>();
builder.Services.AddHostedService<MaintenanceWorker>();
builder.Services.AddProblemDetails();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    o.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = builder.Environment.IsEnvironment("Testing") ? 10000 : 30,
            Window = TimeSpan.FromMinutes(5), QueueLimit = 0 }));
});
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    o.ForwardLimit = 1;
    foreach (var proxy in (builder.Configuration["TrustedProxies"] ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries))
        o.KnownProxies.Add(IPAddress.Parse(proxy.Trim()));
});
builder.WebHost.ConfigureKestrel(o => o.Limits.MaxRequestBodySize = 32 * 1024);

var app = builder.Build();
app.UseForwardedHeaders();
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    context.Response.Headers["Permissions-Policy"] = "geolocation=(), camera=(), microphone=()";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self' data:; connect-src 'self' ws: wss:; font-src 'self'; object-src 'none'; base-uri 'self'; frame-ancestors 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") || context.Request.Path.StartsWithSegments("/hubs"))
        context.Response.Headers.CacheControl = "no-store";
    try { await next(context); }
    catch (RuleException ex) { await Results.Problem(statusCode: ex.Status, title: ex.Message, extensions: new Dictionary<string, object?> { ["code"] = ex.Code }).ExecuteAsync(context); }
    catch (DbUpdateConcurrencyException) { await Results.Problem(statusCode: 409, title: "Има едновременна промяна. Обновете и опитайте отново.", extensions: new Dictionary<string, object?> { ["code"] = "conflict" }).ExecuteAsync(context); }
    catch (DbUpdateException ex) when (ex.InnerException is Npgsql.PostgresException { SqlState: "23505" })
    { await Results.Problem(statusCode: 409, title: "Записът вече съществува. Обновете и опитайте отново.").ExecuteAsync(context); }
    catch (BadHttpRequestException ex) { await Results.Problem(statusCode: ex.StatusCode, title: "Невалидна заявка.").ExecuteAsync(context); }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Request failed: {TraceId}", context.TraceIdentifier);
        await Results.Problem(statusCode: 500, title: "Възникна грешка. Опитайте отново.", extensions: new Dictionary<string, object?> { ["traceId"] = context.TraceIdentifier }).ExecuteAsync(context);
    }
});
if (!app.Environment.IsDevelopment()) app.UseHsts();
app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions { OnPrepareResponse = c =>
{
    if (c.File.Name is "sw.js" or "index.html" or "manifest.webmanifest") c.Context.Response.Headers.CacheControl = "no-cache";
} });
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/api") && !HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) && !HttpMethods.IsOptions(context.Request.Method))
    {
        try { await context.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context); }
        catch (AntiforgeryValidationException) { await Results.Problem(statusCode: 400, title: "Сесията е обновена. Презаредете страницата.", extensions: new Dictionary<string, object?> { ["code"] = "csrf" }).ExecuteAsync(context); return; }
    }
    await next(context);
});
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapGet("/health/ready", async (AppDb db) => await db.Database.CanConnectAsync() ? Results.Ok(new { status = "ready" }) : Results.StatusCode(503));
app.MapAuth();
app.MapGroups();
app.MapLists();
app.MapAdmin();
app.MapHub<EventsHub>("/hubs/events", o => o.CloseOnAuthenticationExpiration = true);
app.Map("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");
await Bootstrap.Initialize(app.Services, app.Configuration);
await app.RunAsync();

public partial class Program;
