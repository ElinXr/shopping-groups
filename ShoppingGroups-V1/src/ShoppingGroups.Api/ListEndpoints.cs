using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public record ProductInput(string Text, bool AllowDuplicate = false);
public record EditProductInput(string Text, Guid Version, bool AllowDuplicate = false);
public record PurchaseInput(bool Purchased, Guid Version);
public record VersionInput(Guid Version);
public record ArchiveInput(bool Archived);

public static class ListEndpoints
{
    static async Task Duplicate(AppDb db, Guid listId, string text, bool allow, Guid? except = null)
    {
        var normalized = Rules.DuplicateKey(text);
        if (!allow && await db.Products.AnyAsync(x => x.ListId == listId && x.Id != except && x.DeletedAt == null && x.NormalizedText == normalized))
            throw new RuleException(409, "duplicate", "Този продукт вече присъства. Можете да го добавите отново.");
    }

    static Product Item(ShoppingList list, Guid id) => list.Items.SingleOrDefault(x => x.Id == id && x.DeletedAt == null)
        ?? throw new RuleException(404, "not_found", "Продуктът не е намерен.");

    public static void MapLists(this WebApplication app)
    {
        var api = app.MapGroup("/api/groups/{groupId:guid}/lists").RequireAuthorization();
        api.MapGet("/", async (Guid groupId, string? view, AppDb db, Collaboration c) =>
        {
            await c.Access(groupId);
            var archived = db.Archives.Where(x => x.UserId == c.UserId).Select(x => x.ListId);
            var query = db.Lists.Where(x => x.GroupId == groupId);
            if (view == "trash") query = query.Where(x => x.DeletedAt != null && x.DeletedAt > DateTime.UtcNow.AddDays(-90));
            else
            {
                query = query.Where(x => x.DeletedAt == null);
                query = view == "archived" ? query.Where(x => archived.Contains(x.Id)) : query.Where(x => !archived.Contains(x.Id));
            }
            return Results.Ok(await query.OrderByDescending(x => x.CreatedAt).Select(x => new {
                x.Id, x.Name, x.Completed, x.CompletedAt, x.DeletedAt,
                total = x.Items.Count(i => i.DeletedAt == null), purchased = x.Items.Count(i => i.DeletedAt == null && i.Purchased),
                shoppers = db.Sessions.Count(s => s.ListId == x.Id && s.EndedAt == null)
            }).ToListAsync());
        });

        api.MapPost("/", async (Guid groupId, NameInput input, AppDb db, Collaboration c) =>
        {
            await c.Access(groupId);
            var list = new ShoppingList { GroupId = groupId, Name = Rules.Text(input.Name, "Списък", 100) };
            db.Lists.Add(list);
            await c.Log(groupId, list.Id, "list.created", $"Създаден списък „{list.Name}“.");
            await c.Save(groupId);
            return Results.Ok(new { list.Id });
        });

        api.MapGet("/{listId:guid}", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            return Results.Ok(new {
                list.Id, list.GroupId, list.Name, list.Completed, list.CompletedAt,
                archived = await db.Archives.AnyAsync(x => x.ListId == listId && x.UserId == c.UserId),
                items = list.Items.Where(x => x.DeletedAt == null).OrderBy(x => x.CreatedAt)
                    .Select(x => new { x.Id, x.Text, x.Version, x.Purchased, x.PurchasedBy, x.PurchasedAt }),
                shoppers = await db.Sessions.Where(x => x.ListId == listId && x.EndedAt == null)
                    .Select(x => new { x.UserId, x.User.DisplayName, x.StartedAt }).ToListAsync()
            });
        });

        api.MapPut("/{listId:guid}", async (Guid groupId, Guid listId, NameInput input, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            list.Name = Rules.Text(input.Name, "Списък", 100);
            await c.Log(groupId, listId, "list.renamed", $"Списъкът вече е „{list.Name}“.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapDelete("/{listId:guid}", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            list.DeletedAt = DateTime.UtcNow;
            list.RunStarted = false;
            foreach (var session in await db.Sessions.Where(x => x.ListId == listId && x.EndedAt == null).ToListAsync()) session.EndedAt = DateTime.UtcNow;
            await c.Log(groupId, listId, "list.deleted", $"Списъкът „{list.Name}“ е изтрит. Възстановяване до 90 дни.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/{listId:guid}/restore", async (Guid groupId, Guid listId, Collaboration c) =>
        {
            var list = await c.List(groupId, listId, true);
            if (!Rules.CanRestore(list.DeletedAt, DateTime.UtcNow)) throw new RuleException(410, "restore_expired", "Срокът за възстановяване е изтекъл или списъкът не е изтрит.");
            list.DeletedAt = null;
            await c.Log(groupId, listId, "list.restored", $"Списъкът „{list.Name}“ е възстановен.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPut("/{listId:guid}/archive", async (Guid groupId, Guid listId, ArchiveInput input, AppDb db, Collaboration c) =>
        {
            await c.List(groupId, listId);
            var archive = await db.Archives.SingleOrDefaultAsync(x => x.ListId == listId && x.UserId == c.UserId);
            if (input.Archived && archive == null) db.Archives.Add(new ListArchive { ListId = listId, UserId = c.UserId });
            else if (!input.Archived && archive != null) db.Archives.Remove(archive);
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/{listId:guid}/items", async (Guid groupId, Guid listId, ProductInput input, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var text = Rules.Text(input.Text, "Продукт");
            await Duplicate(db, listId, text, input.AllowDuplicate);
            var item = new Product { ListId = listId, Text = text, NormalizedText = Rules.DuplicateKey(text), AddedBy = c.UserId };
            list.Items.Add(item);
            db.Products.Add(item);
            await c.Log(groupId, listId, "item.added", $"Добавено: {text}", item.CreatedAt);
            await c.Recompute(list);
            await c.Save(groupId);
            return Results.Ok(new { item.Id, item.Version });
        });

        api.MapPut("/{listId:guid}/items/{itemId:guid}", async (Guid groupId, Guid listId, Guid itemId, EditProductInput input, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var item = Item(list, itemId);
            Rules.CheckVersion(item.Version, input.Version);
            var text = Rules.Text(input.Text, "Продукт");
            await Duplicate(db, listId, text, input.AllowDuplicate, itemId);
            var old = item.Text;
            item.Text = text;
            item.NormalizedText = Rules.DuplicateKey(text);
            item.Version = Guid.NewGuid();
            await c.Log(groupId, listId, "item.edited", $"Променено: {old} → {text}");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPut("/{listId:guid}/items/{itemId:guid}/purchase", async (Guid groupId, Guid listId, Guid itemId, PurchaseInput input, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var item = Item(list, itemId);
            Rules.CheckVersion(item.Version, input.Version);
            if (item.Purchased == input.Purchased) return Results.NoContent();
            item.Purchased = input.Purchased;
            item.PurchasedBy = input.Purchased ? c.UserId : null;
            item.PurchasedAt = input.Purchased ? DateTime.UtcNow : null;
            item.Version = Guid.NewGuid();
            await c.Log(groupId, listId, input.Purchased ? "item.purchased" : "item.unpurchased", $"{(input.Purchased ? "Купено" : "Върнато за покупка")}: {item.Text}");
            await c.Recompute(list);
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapDelete("/{listId:guid}/items/{itemId:guid}", async (Guid groupId, Guid listId, Guid itemId, Guid version, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var item = Item(list, itemId);
            Rules.CheckVersion(item.Version, version);
            item.DeletedAt = DateTime.UtcNow;
            item.Version = Guid.NewGuid();
            await c.Log(groupId, listId, "item.deleted", $"Изтрито: {item.Text}");
            await c.Recompute(list);
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/{listId:guid}/shopping/start", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            if (list.Completed || !list.Items.Any(x => x.DeletedAt == null && !x.Purchased))
                throw new RuleException(400, "nothing_to_buy", "Няма продукти за покупка.");
            if (await db.Sessions.AnyAsync(x => x.ListId == listId && x.UserId == c.UserId && x.EndedAt == null)) return Results.NoContent();
            db.Sessions.Add(new ShoppingSession { ListId = listId, UserId = c.UserId });
            await c.Log(groupId, listId, "shopping.started", "Участник започна да пазарува.");
            if (!list.RunStarted)
            {
                list.RunStarted = true;
                await c.Notify(list, "started", $"Започна пазаруването по „{list.Name}“.");
            }
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapPost("/{listId:guid}/shopping/stop", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var sessions = await db.Sessions.Where(x => x.ListId == listId && x.EndedAt == null).ToListAsync();
            var own = sessions.SingleOrDefault(x => x.UserId == c.UserId);
            if (own == null) return Results.NoContent();
            own.EndedAt = DateTime.UtcNow;
            if (sessions.All(x => x.EndedAt != null)) list.RunStarted = false;
            await c.Log(groupId, listId, "shopping.stopped", "Участник приключи своето пазаруване.");
            await c.Save(groupId);
            return Results.NoContent();
        });

        api.MapGet("/{listId:guid}/summary", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var changes = await db.Activities.Where(x => x.ListId == listId && x.Id > list.LastSummaryActivityId && Rules.ProductChanges.Contains(x.Kind))
                .OrderBy(x => x.Id).ToListAsync();
            return Results.Ok(new { count = changes.Count, summary = changes.Count == 0 ? "" : NotificationText.Summary(changes.Select(x => (x.Kind, x.Text, x.ActorName, x.At))),
                through = changes.Count == 0 ? list.LastSummaryActivityId : changes[^1].Id });
        });

        api.MapPost("/{listId:guid}/notify", async (Guid groupId, Guid listId, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var changes = await db.Activities.Where(x => x.ListId == listId && x.Id > list.LastSummaryActivityId && Rules.ProductChanges.Contains(x.Kind))
                .OrderBy(x => x.Id).ToListAsync();
            var summary = NotificationText.Summary(changes.Select(x => (x.Kind, x.Text, x.ActorName, x.At)));
            await c.Notify(list, "summary", $"Промени в „{list.Name}“\n{summary}");
            list.LastSummaryActivityId = changes[^1].Id;
            await c.Log(groupId, listId, "summary.sent", "Изпратено обобщение на последните промени.");
            await c.Save(groupId);
            return Results.NoContent();
        });
    }
}
