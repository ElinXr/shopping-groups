using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public record NameInput(string Name);
public record InviteInput(string? Email, int MaxUses = 1, int ExpiresInDays = 7);
public record AcceptInput(string Token);
public record TransferInput(string UserId);

public static class GroupEndpoints
{
    static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static void MapGroups(this WebApplication app)
    {
        var api = app.MapGroup("/api").RequireAuthorization();
        api.MapGet("/groups", async (AppDb db, Collaboration c) => Results.Ok(await db.Members
            .Where(x => x.UserId == c.UserId).OrderBy(x => x.Group.Name)
            .Select(x => new { x.Group.Id, x.Group.Name, x.Role, x.Group.Disabled, x.Group.Version }).ToListAsync()));

        api.MapPost("/groups", async (NameInput input, AppDb db, Collaboration c) =>
        {
            var group = new Group { Name = Rules.Text(input.Name, "Група", 100) };
            db.Groups.Add(group);
            db.Members.Add(new Member { Group = group, UserId = c.UserId, Role = "owner" });
            await c.Log(group.Id, null, "group.created", $"Създадена група „{group.Name}“.");
            await db.SaveChangesAsync();
            return Results.Ok(new { group.Id });
        });

        api.MapGet("/groups/{groupId:guid}", async (Guid groupId, AppDb db, Collaboration c) =>
        {
            var (group, me) = await c.Access(groupId);
            var members = await db.Members.Where(x => x.GroupId == groupId)
                .Select(x => new { x.UserId, x.Role, x.User.DisplayName }).ToListAsync();
            return Results.Ok(new { group.Id, group.Name, group.Version, role = me.Role, members });
        });

        api.MapPut("/groups/{groupId:guid}", async (Guid groupId, NameInput input, Collaboration c) =>
        {
            var (group, _) = await c.Access(groupId, true);
            group.Name = Rules.Text(input.Name, "Група", 100);
            await c.Log(groupId, null, "group.renamed", $"Групата вече е „{group.Name}“.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/groups/{groupId:guid}/transfer", async (Guid groupId, TransferInput input, AppDb db, Collaboration c) =>
        {
            var (_, owner) = await c.Access(groupId, true);
            var successor = await db.Members.SingleOrDefaultAsync(x => x.GroupId == groupId && x.UserId == input.UserId)
                ?? throw new RuleException(404, "not_found", "Членът не е намерен.");
            if (successor.UserId == c.UserId) return Results.NoContent();
            owner.Role = "member";
            successor.Role = "owner";
            await c.Log(groupId, null, "group.transferred", "Собствеността на групата е прехвърлена.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapDelete("/groups/{groupId:guid}/members/{userId}", async (Guid groupId, string userId, AppDb db, Collaboration c) =>
        {
            var (_, me) = await c.Access(groupId);
            if (me.Role != "owner" && userId != c.UserId) throw new RuleException(403, "forbidden", "Не можете да премахвате други членове.");
            var member = await db.Members.SingleOrDefaultAsync(x => x.GroupId == groupId && x.UserId == userId)
                ?? throw new RuleException(404, "not_found", "Членът не е намерен.");
            if (member.Role == "owner") throw new RuleException(400, "transfer_required", "Първо прехвърлете собствеността.");
            db.Members.Remove(member);
            foreach (var session in await db.Sessions.Where(x => x.List.GroupId == groupId && x.UserId == userId && x.EndedAt == null).ToListAsync()) session.EndedAt = DateTime.UtcNow;
            foreach (var list in await db.Lists.Where(x => x.GroupId == groupId && x.RunStarted).ToListAsync())
                if (!await db.Sessions.AnyAsync(x => x.ListId == list.Id && x.EndedAt == null && x.UserId != userId)) list.RunStarted = false;
            db.Archives.RemoveRange(await db.Archives.Where(x => x.List.GroupId == groupId && x.UserId == userId).ToListAsync());
            db.Notifications.RemoveRange(await db.Notifications.Where(x => x.GroupId == groupId && x.UserId == userId).ToListAsync());
            db.Emails.RemoveRange(await db.Emails.Where(x => x.GroupId == groupId && x.UserId == userId && x.SentAt == null).ToListAsync());
            await c.Log(groupId, null, "member.removed", "Член напусна или беше премахнат от групата.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapGet("/groups/{groupId:guid}/invitations", async (Guid groupId, AppDb db, Collaboration c) =>
        {
            await c.Access(groupId, true);
            return Results.Ok(await db.Invitations.Where(x => x.GroupId == groupId).OrderByDescending(x => x.CreatedAt)
                .Select(x => new { x.Id, x.Email, x.ExpiresAt, x.RevokedAt, x.MaxUses, x.Uses }).Take(100).ToListAsync());
        });

        api.MapPost("/groups/{groupId:guid}/invitations", async (Guid groupId, InviteInput input, AppDb db, Collaboration c, IConfiguration config) =>
        {
            var (group, _) = await c.Access(groupId, true);
            if (input.MaxUses is < 1 or > 100 || input.ExpiresInDays is < 1 or > 30)
                throw new RuleException(400, "validation", "Валидност 1–30 дни и 1–100 използвания.");
            string? email = null;
            if (!string.IsNullOrWhiteSpace(input.Email))
            {
                email = input.Email.Trim().ToLowerInvariant();
                if (email.Length > 254 || !MailAddress.TryCreate(email, out var address) || address.Address != email)
                    throw new RuleException(400, "validation", "Невалиден имейл.");
            }
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var invitation = new Invitation { GroupId = groupId, TokenHash = Hash(token), Email = email,
                MaxUses = email != null ? 1 : input.MaxUses, ExpiresAt = DateTime.UtcNow.AddDays(input.ExpiresInDays) };
            db.Invitations.Add(invitation);
            var url = $"{config["PublicUrl"]!.TrimEnd('/')}/?invite={token}";
            if (email != null) db.Emails.Add(new EmailMessage { To = email, Subject = "Покана за Shopping Groups",
                Body = $"Поканени сте в „{group.Name}“.\n\n{url}\n\nЛинкът е валиден до {invitation.ExpiresAt:u}." });
            await c.Log(groupId, null, "invitation.created", email != null ? "Създадена е покана по имейл." : "Създадена е покана с линк/QR.");
            await c.Save(groupId);
            return Results.Ok(new { invitation.Id, url, invitation.ExpiresAt });
        });

        api.MapDelete("/groups/{groupId:guid}/invitations/{invitationId:guid}", async (Guid groupId, Guid invitationId, AppDb db, Collaboration c) =>
        {
            await c.Access(groupId, true);
            var invite = await db.Invitations.SingleOrDefaultAsync(x => x.Id == invitationId && x.GroupId == groupId)
                ?? throw new RuleException(404, "not_found", "Поканата не е намерена.");
            invite.RevokedAt = DateTime.UtcNow;
            await c.Log(groupId, null, "invitation.revoked", "Поканата е отменена.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/invitations/accept", async (AcceptInput input, AppDb db, Collaboration c) =>
        {
            if (input.Token is not { Length: 64 }) throw new RuleException(400, "invalid_invitation", "Невалидна покана.");
            var hash = Hash(input.Token);
            var invite = await db.Invitations.Include(x => x.Group).SingleOrDefaultAsync(x => x.TokenHash == hash);
            if (invite == null || invite.RevokedAt != null || invite.ExpiresAt <= DateTime.UtcNow || invite.Uses >= invite.MaxUses || invite.Group.Disabled)
                throw new RuleException(400, "invalid_invitation", "Поканата е изтекла, отменена или изчерпана.");
            var user = await db.Users.SingleAsync(x => x.Id == c.UserId);
            if (invite.Email != null && (!user.EmailConfirmed || !string.Equals(invite.Email, user.Email, StringComparison.OrdinalIgnoreCase)))
                throw new RuleException(403, "email_required", "Влезте с потвърдения имейл, до който е изпратена поканата.");
            if (await db.Members.AnyAsync(x => x.GroupId == invite.GroupId && x.UserId == c.UserId)) return Results.Ok(new { groupId = invite.GroupId });
            invite.Uses++;
            db.Members.Add(new Member { GroupId = invite.GroupId, UserId = c.UserId });
            await c.Log(invite.GroupId, null, "member.joined", $"{user.DisplayName} се присъедини към групата.");
            await c.Save(invite.GroupId);
            return Results.Ok(new { groupId = invite.GroupId });
        }).RequireRateLimiting("auth");

        api.MapGet("/groups/{groupId:guid}/activity", async (Guid groupId, long? before, Guid? listId, AppDb db, Collaboration c) =>
        {
            await c.Access(groupId);
            return Results.Ok(await db.Activities.Where(x => x.GroupId == groupId && (before == null || x.Id < before) && (listId == null || x.ListId == listId))
                .OrderByDescending(x => x.Id).Take(50).Select(x => new { x.Id, x.ListId, x.ActorName, x.Kind, x.Text, x.At }).ToListAsync());
        });
    }
}
