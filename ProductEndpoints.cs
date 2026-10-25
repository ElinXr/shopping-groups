using Microsoft.EntityFrameworkCore;
using ShoppingGroups.Domain;

namespace ShoppingGroups.Api;

public record ProductInput(string Text, bool AllowDuplicate = false, decimal? Quantity = null, string? Unit = null);
public record EditProductInput(string Text, Guid Version, bool AllowDuplicate = false, decimal? Quantity = null, string? Unit = null);
public record PurchaseInput(bool Purchased, Guid Version);

public static class ProductEndpoints
{
    static async Task Duplicate(AppDb db, Guid listId, string text, bool allow, Guid? except = null)
    {
        var normalized = Rules.DuplicateKey(text);
        if (!allow && await db.Products.AnyAsync(x => x.ListId == listId && x.Id != except && x.DeletedAt == null && x.NormalizedText == normalized))
            throw new RuleException(409, "duplicate", "Този продукт вече присъства. Можете да го добавите отново.");
    }

    static Product Item(ShoppingList list, Guid id) => list.Items.SingleOrDefault(x => x.Id == id && x.DeletedAt == null)
        ?? throw new RuleException(404, "not_found", "Продуктът не е намерен.");

    public static void MapProducts(this RouteGroupBuilder api)
    {
        api.MapPost("/{listId:guid}/items", async (Guid groupId, Guid listId, ProductInput input, AppDb db, Collaboration c) =>
        {
            var list = await c.List(groupId, listId);
            var text = Rules.Text(input.Text, "Продукт");
            var amount = ProductAmount.Create(input.Quantity ?? 1, input.Unit ?? "pcs");
            await Duplicate(db, listId, text, input.AllowDuplicate);
            var item = new Product { ListId = listId, Text = text, Quantity = amount.Quantity, Unit = amount.Unit, NormalizedText = Rules.DuplicateKey(text), AddedBy = c.UserId };
            list.Items.Add(item);
            db.Products.Add(item);
            await c.Log(groupId, listId, "item.added", $"Добавено: {amount.Describe(text)}", item.CreatedAt);
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
            var amount = ProductAmount.Create(input.Quantity ?? item.Quantity, input.Unit ?? item.Unit);
            if (item.Text == text && item.Quantity == amount.Quantity && item.Unit == amount.Unit) return Results.NoContent();
            await Duplicate(db, listId, text, input.AllowDuplicate, itemId);
            var old = ProductAmount.Create(item.Quantity, item.Unit).Describe(item.Text);
            // A changed request needs a fresh purchase confirmation, including a completed list.
            item.Purchased = false;
            item.PurchasedBy = null;
            item.PurchasedAt = null;
            item.Text = text;
            item.Quantity = amount.Quantity;
            item.Unit = amount.Unit;
            item.NormalizedText = Rules.DuplicateKey(text);
            item.Version = Guid.NewGuid();
            await c.Log(groupId, listId, "item.edited", $"Променено: {old} → {amount.Describe(text)}");
            await c.Recompute(list);
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
            await c.Log(groupId, listId, input.Purchased ? "item.purchased" : "item.unpurchased", $"{(input.Purchased ? "Купено" : "Върнато за покупка")}: {ProductAmount.Create(item.Quantity, item.Unit).Describe(item.Text)}");
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
            await c.Log(groupId, listId, "item.deleted", $"Изтрито: {ProductAmount.Create(item.Quantity, item.Unit).Describe(item.Text)}");
            await c.Recompute(list);
            await c.Save(groupId);
            return Results.NoContent();
        });
    }
}
