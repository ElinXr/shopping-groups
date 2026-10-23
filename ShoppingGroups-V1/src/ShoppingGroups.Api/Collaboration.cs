using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

[Authorize]
public sealed class EventsHub : Hub
{
    // User groups are assigned only by the server. Removed members receive no future group events.
    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
        await base.OnConnectedAsync();
    }
}

public sealed class Collaboration(AppDb db, IHttpContextAccessor accessor, IHubContext<EventsHub> hub, ILogger<Collaboration> logger)
{
    public string UserId => accessor.HttpContext!.User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<(Group Group, Member Member)> Access(Guid groupId, bool owner = false)
    {
        var member = await db.Members.Include(x => x.Group).Include(x => x.User)
            .SingleOrDefaultAsync(x => x.GroupId == groupId && x.UserId == UserId);
        if (member == null) throw new RuleException(404, "not_found", "Групата не е намерена.");
        if (member.Group.Disabled) throw new RuleException(403, "group_disabled", "Групата е спряна от администратор.");
        if (owner && member.Role != "owner") throw new RuleException(403, "owner_required", "Действието е достъпно за собственика.");
        return (member.Group, member);
    }

    public async Task<ShoppingList> List(Guid groupId, Guid listId, bool deleted = false)
    {
        await Access(groupId);
        var list = await db.Lists.Include(x => x.Items).SingleOrDefaultAsync(x => x.Id == listId && x.GroupId == groupId);
        if (list == null || (list.DeletedAt.HasValue && !deleted))
            throw new RuleException(404, "not_found", "Списъкът не е намерен.");
        return list;
    }

    public async Task Log(Guid groupId, Guid? listId, string kind, string text)
    {
        var user = await db.Users.SingleAsync(x => x.Id == UserId);
        db.Activities.Add(new Activity { GroupId = groupId, ListId = listId, Kind = kind, Text = text,
            ActorId = UserId, ActorName = user.DisplayName });
    }

    public async Task Notify(ShoppingList list, string kind, string text)
    {
        var users = await db.Members.Where(x => x.GroupId == list.GroupId).Select(x => x.User).ToListAsync();
        foreach (var user in users.Where(x => !x.Disabled))
        {
            db.Notifications.Add(new Notification { GroupId = list.GroupId, ListId = list.Id, UserId = user.Id, Kind = kind, Text = text });
            if (user.EmailNotifications && user.EmailConfirmed)
                db.Emails.Add(new EmailMessage { GroupId = list.GroupId, UserId = user.Id, To = user.Email!, Subject = $"Shopping Groups · {list.Name}", Body = text });
        }
    }

    public async Task Recompute(ShoppingList list)
    {
        var completed = Rules.Completed(list.Items.Where(x => x.DeletedAt == null).Select(x => x.Purchased));
        if (completed && !list.Completed)
        {
            list.Completed = true;
            list.CompletedAt = DateTime.UtcNow;
            list.RunStarted = false;
            foreach (var session in await db.Sessions.Where(x => x.ListId == list.Id && x.EndedAt == null).ToListAsync())
                session.EndedAt = DateTime.UtcNow;
            await Log(list.GroupId, list.Id, "list.completed", $"Списъкът „{list.Name}“ е завършен.");
            await Notify(list, "completed", $"Всичко от „{list.Name}“ е купено.");
        }
        else if (!completed && list.Completed)
        {
            list.Completed = false;
            list.CompletedAt = null;
            list.RunStarted = false;
            await Log(list.GroupId, list.Id, "list.reactivated", $"Списъкът „{list.Name}“ отново е активен.");
        }
    }

    public async Task Save(Guid groupId)
    {
        // Every group mutation updates this concurrency token in the SAME transaction as its effects.
        var group = await db.Groups.SingleAsync(x => x.Id == groupId);
        group.Version = Guid.NewGuid();
        await db.SaveChangesAsync();
        await Publish(groupId);
    }

    public async Task Publish(Guid groupId)
    {
        try
        {
            var members = await db.Members.Where(x => x.GroupId == groupId && !x.User.Disabled).Select(x => x.UserId).ToListAsync();
            await hub.Clients.Groups(members.Select(x => $"user:{x}").ToList()).SendAsync("Changed", new { groupId });
        }
        catch (Exception ex) { logger.LogWarning(ex, "Realtime invalidation failed after commit for {GroupId}; clients recover by polling", groupId); }
    }
}
