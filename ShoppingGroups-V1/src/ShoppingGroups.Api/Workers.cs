using System.Net;
using System.Net.Mail;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ShoppingGroups.Api;

public sealed class MaintenanceWorker(IServiceScopeFactory scopes, IConfiguration config, ILogger<MaintenanceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(Math.Clamp(config.GetValue("Maintenance:IntervalSeconds", 10), 10, 3600)));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDb>();
                // A PostgreSQL advisory lock makes delivery/cleanup safe if a second API replica starts.
                await using var connection = db.Database.GetDbConnection();
                await connection.OpenAsync(stoppingToken);
                await using var acquire = connection.CreateCommand();
                acquire.CommandText = "SELECT pg_try_advisory_lock(735091245)";
                if (!Equals(await acquire.ExecuteScalarAsync(stoppingToken), true)) continue;
                try
                {
                    var pending = await db.Emails.Where(x => x.SentAt == null && x.Attempts < 10 && x.AvailableAt <= DateTime.UtcNow).OrderBy(x => x.Id).Take(20).ToListAsync(stoppingToken);
                    foreach (var email in pending)
                    {
                        if (email.GroupId != null && email.UserId != null && !await db.Members.AnyAsync(x => x.GroupId == email.GroupId && x.UserId == email.UserId && !x.User.Disabled && !x.Group.Disabled && x.User.EmailNotifications, stoppingToken))
                        {
                            db.Emails.Remove(email);
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }
                        try
                        {
                            using var smtp = new SmtpClient(config["Smtp:Host"] ?? "localhost", config.GetValue("Smtp:Port", 1025))
                            { EnableSsl = config.GetValue("Smtp:EnableSsl", false), Timeout = 15000 };
                            if (!string.IsNullOrEmpty(config["Smtp:Username"])) smtp.Credentials = new NetworkCredential(config["Smtp:Username"], config["Smtp:Password"]);
                            using var message = new MailMessage(config["Smtp:From"] ?? "shopping@localhost", email.To, email.Subject, email.Body);
                            await smtp.SendMailAsync(message, stoppingToken);
                            email.SentAt = DateTime.UtcNow;
                            email.LastError = null;
                        }
                        catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
                        {
                            email.Attempts++;
                            // Do not retain server responses containing recipient or message secrets.
                            email.LastError = ex.GetType().Name;
                            email.AvailableAt = DateTime.UtcNow.AddMinutes(Math.Min(360, Math.Pow(2, email.Attempts)));
                            logger.LogWarning("Email outbox entry {Id} failed, attempt {Attempt}", email.Id, email.Attempts);
                        }
                        await db.SaveChangesAsync(stoppingToken);
                    }
                    var expired = await db.Lists.Where(x => x.DeletedAt != null && x.DeletedAt <= DateTime.UtcNow.AddDays(-90)).Select(x => x.Id).ToListAsync(stoppingToken);
                    if (expired.Count > 0)
                    {
                        await db.Notifications.Where(x => x.ListId != null && expired.Contains(x.ListId.Value)).ExecuteDeleteAsync(stoppingToken);
                        await db.Activities.Where(x => x.ListId != null && expired.Contains(x.ListId.Value)).ExecuteDeleteAsync(stoppingToken);
                        await db.Lists.Where(x => expired.Contains(x.Id)).ExecuteDeleteAsync(stoppingToken);
                    }
                    await db.Emails.Where(x => x.SentAt != null && x.SentAt < DateTime.UtcNow.AddDays(-7)).ExecuteDeleteAsync(stoppingToken);
                }
                finally
                {
                    await using var release = connection.CreateCommand();
                    release.CommandText = "SELECT pg_advisory_unlock(735091245)";
                    await release.ExecuteScalarAsync(CancellationToken.None);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Maintenance pass failed"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}

public static class Bootstrap
{
    public static async Task Initialize(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDb>();
        if (config.GetValue("Database:MigrateOnStartup", true)) await db.Database.MigrateAsync();
        var email = config["Bootstrap:AdminEmail"];
        var password = config["Bootstrap:AdminPassword"];
        if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password)) return;
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        if (!await roles.RoleExistsAsync("Admin"))
        {
            var roleResult = await roles.CreateAsync(new IdentityRole("Admin"));
            if (!roleResult.Succeeded) throw new InvalidOperationException("Could not create admin role.");
        }
        var admin = await users.FindByEmailAsync(email);
        if (admin == null)
        {
            admin = new AppUser { Email = email, UserName = email, DisplayName = "Администратор", EmailConfirmed = true };
            var result = await users.CreateAsync(admin, password);
            if (!result.Succeeded) throw new InvalidOperationException("Admin bootstrap failed: " + string.Join(" ", result.Errors.Select(x => x.Description)));
        }
        if (!await users.IsInRoleAsync(admin, "Admin"))
        {
            // Never promote an existing public account merely because it matches an environment address.
            if (!admin.EmailConfirmed || !await users.CheckPasswordAsync(admin, password))
                throw new InvalidOperationException("Existing bootstrap account must be confirmed and match the configured password.");
            var result = await users.AddToRoleAsync(admin, "Admin");
            if (!result.Succeeded) throw new InvalidOperationException("Admin role assignment failed.");
        }
    }
}
