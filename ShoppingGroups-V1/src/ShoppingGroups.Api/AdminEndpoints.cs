using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public record DisabledInput(bool Disabled);

public static class AdminEndpoints
{
    public static void MapAdmin(this WebApplication app)
    {
        var api = app.MapGroup("/api/admin").RequireAuthorization(p => p.RequireRole("Admin"));
        api.MapGet("/overview", async (AppDb db) => Results.Ok(new {
            users = await db.Users.CountAsync(), groups = await db.Groups.CountAsync(), lists = await db.Lists.CountAsync(),
            activeShoppers = await db.Sessions.CountAsync(x => x.EndedAt == null),
            pendingEmails = await db.Emails.CountAsync(x => x.SentAt == null),
            failedEmails = await db.Emails.CountAsync(x => x.SentAt == null && x.Attempts >= 10)
        }));
        api.MapGet("/users", async (string? q, int? page, AppDb db) => Results.Ok(await db.Users
            .Where(x => q == null || x.Email!.Contains(q) || x.DisplayName.Contains(q)).OrderBy(x => x.CreatedAt)
            .Skip(Math.Clamp(page ?? 0, 0, 100000) * 50).Take(50)
            .Select(x => new { x.Id, x.Email, x.DisplayName, x.Disabled, x.EmailConfirmed, x.CreatedAt }).ToListAsync()));
        api.MapGet("/groups", async (int? page, AppDb db) => Results.Ok(await db.Groups.OrderBy(x => x.CreatedAt)
            .Skip(Math.Clamp(page ?? 0, 0, 100000) * 50).Take(50)
            .Select(x => new { x.Id, x.Name, x.Disabled, members = db.Members.Count(m => m.GroupId == x.Id) }).ToListAsync()));
        api.MapPut("/users/{userId}/disabled", async (string userId, DisabledInput input, ClaimsPrincipal principal, AppDb db, UserManager<AppUser> users, Collaboration c) =>
        {
            if (userId == principal.FindFirstValue(ClaimTypes.NameIdentifier)) throw new RuleException(400, "self_disable", "Не можете да спрете собствения си профил.");
            await using var transaction = await db.Database.BeginTransactionAsync();
            var user = await users.FindByIdAsync(userId) ?? throw new RuleException(404, "not_found", "Профилът не е намерен.");
            var groups = await db.Groups.Where(g => db.Members.Any(m => m.GroupId == g.Id && m.UserId == userId)).ToListAsync();
            foreach (var group in groups) group.Version = Guid.NewGuid();
            user.Disabled = input.Disabled;
            var updated = await users.UpdateSecurityStampAsync(user);
            if (!updated.Succeeded) throw new RuleException(409, "conflict", "Профилът е променен. Обновете и опитайте отново.");
            foreach (var session in await db.Sessions.Where(x => x.UserId == userId && x.EndedAt == null).ToListAsync()) session.EndedAt = DateTime.UtcNow;
            foreach (var list in await db.Lists.Where(x => x.RunStarted && db.Sessions.Any(s => s.ListId == x.Id && s.UserId == userId && s.EndedAt == null)).ToListAsync())
                if (!await db.Sessions.AnyAsync(x => x.ListId == list.Id && x.EndedAt == null && x.UserId != userId)) list.RunStarted = false;
            db.AdminAudits.Add(new AdminAudit { ActorId = principal.FindFirstValue(ClaimTypes.NameIdentifier)!, Action = input.Disabled ? "user.disabled" : "user.enabled", Target = userId });
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            foreach (var group in groups) await c.Publish(group.Id);
            return Results.NoContent();
        });
        api.MapPut("/groups/{groupId:guid}/disabled", async (Guid groupId, DisabledInput input, AppDb db, Collaboration c) =>
        {
            var group = await db.Groups.SingleOrDefaultAsync(x => x.Id == groupId) ?? throw new RuleException(404, "not_found", "Групата не е намерена.");
            group.Disabled = input.Disabled;
            if (input.Disabled)
            {
                foreach (var session in await db.Sessions.Where(x => x.List.GroupId == groupId && x.EndedAt == null).ToListAsync()) session.EndedAt = DateTime.UtcNow;
                foreach (var list in await db.Lists.Where(x => x.GroupId == groupId).ToListAsync()) list.RunStarted = false;
            }
            db.AdminAudits.Add(new AdminAudit { ActorId = c.UserId, Action = input.Disabled ? "group.disabled" : "group.enabled", Target = groupId.ToString() });
            await c.Save(groupId);
            return Results.NoContent();
        });
        api.MapGet("/audit", async (long? before, AppDb db) => Results.Ok(await db.AdminAudits.Where(x => before == null || x.Id < before)
            .OrderByDescending(x => x.Id).Take(50).ToListAsync()));
        api.MapGet("/emails", async (AppDb db) => Results.Ok(await db.Emails.Where(x => x.SentAt == null).OrderBy(x => x.Id).Take(100)
            .Select(x => new { x.Id, x.To, x.Subject, x.Attempts, x.LastError, x.AvailableAt }).ToListAsync()));
        api.MapPost("/emails/{id:long}/retry", async (long id, AppDb db, Collaboration c) =>
        {
            var email = await db.Emails.SingleOrDefaultAsync(x => x.Id == id && x.SentAt == null) ?? throw new RuleException(404, "not_found", "Съобщението не е намерено.");
            email.Attempts = 0;
            email.AvailableAt = DateTime.UtcNow;
            email.LastError = null;
            db.AdminAudits.Add(new AdminAudit { ActorId = c.UserId, Action = "email.retried", Target = id.ToString() });
            await db.SaveChangesAsync();
            return Results.NoContent();
        });

        var notifications = app.MapGroup("/api/notifications").RequireAuthorization();
        notifications.MapGet("/", async (long? before, AppDb db, Collaboration c) => Results.Ok(await db.Notifications
            .Where(x => x.UserId == c.UserId && (before == null || x.Id < before) && db.Members.Any(m => m.UserId == c.UserId && m.GroupId == x.GroupId && !m.Group.Disabled))
            .OrderByDescending(x => x.Id).Take(50).Select(x => new { x.Id, x.GroupId, x.ListId, x.Kind, x.Text, x.At, x.ReadAt }).ToListAsync()));
        notifications.MapPut("/{id:long}/read", async (long id, AppDb db, Collaboration c) =>
        {
            var n = await db.Notifications.SingleOrDefaultAsync(x => x.Id == id && x.UserId == c.UserId)
                ?? throw new RuleException(404, "not_found", "Известието не е намерено.");
            n.ReadAt ??= DateTime.UtcNow;
            await db.SaveChangesAsync();
            return Results.NoContent();
        });
    }
}
