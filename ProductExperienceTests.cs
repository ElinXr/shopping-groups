using System.Text.Json.Nodes;

// Independent account and list: these cases do not alter the shared push/session fixture.
static class ProductExperienceTests
{
    public static async Task Run(string baseUrl, Func<string, Func<Task>, Task> test)
    {
        using var user = new Client(baseUrl);
        var suffix = Guid.NewGuid().ToString("N");
        await user.Register($"amount-{suffix}@example.test", "Amount-Test-42!", "Мария");
        var group = (await user.Call("POST", "/groups", new { name = "Количества" }))!["id"]!.GetValue<string>();
        var root = $"/groups/{group}/lists";
        var list = (await user.Call("POST", root + "/", new { name = "За дома" }))!["id"]!.GetValue<string>();
        var path = root + "/" + list;
        var legacyList = (await user.Call("POST", root + "/", new { name = "Стари клиенти" }))!["id"]!.GetValue<string>();
        await test("Legacy product requests still create one piece without altering their text", async () => {
            var groups = (await user.Call("GET", "/groups"))!.AsArray();
            Check(groups.Single(g => g!["id"]!.GetValue<string>() == group)!["memberCount"]!.GetValue<int>() == 1, "Group picker has a member count.");
            var legacy = (await user.Call("POST", root + "/" + legacyList + "/items", new { text = "Мляко 2 л" }))!["id"]!.GetValue<string>();
            var saved = await user.Item(root + "/" + legacyList, legacy);
            Check(saved["quantity"]!.GetValue<decimal>() == 1 && saved["unit"]!.GetValue<string>() == "pcs", "Backward compatible amount.");
            Check(saved["text"]!.GetValue<string>() == "Мляко 2 л", "Existing free text is preserved.");
        });
        var id = "";
        await test("Fractional amounts round-trip through PostgreSQL and appear in activity", async () => {
            id = (await user.Call("POST", path + "/items", new { text = "Ябълки", quantity = 1.5m, unit = "kg" }))!["id"]!.GetValue<string>();
            var saved = await user.Item(path, id);
            Check(saved["quantity"]!.GetValue<decimal>() == 1.5m && saved["unit"]!.GetValue<string>() == "kg", "Fractional amount retained.");
            var history = (await user.Call("GET", $"/groups/{group}/activity?listId={list}"))!.AsArray();
            Check(history.Any(x => x!["text"]!.GetValue<string>().Contains("1,5 кг")), "Activity includes amount.");
        });
        await test("The API rejects invalid amounts and units without inserting products", async () => {
            foreach (var quantity in new[] { 0m, -1m, 0.0001m, 1000000m })
                await user.Call("POST", path + "/items", new { text = "Невалиден", quantity, unit = "pcs" }, 400);
            await user.Call("POST", path + "/items", new { text = "Невалиден", quantity = 1, unit = "unknown" }, 400);
            Check((await user.Call("GET", path))!["items"]!.AsArray().Count == 1, "Invalid products were not saved.");
        });
        await test("Older edit clients retain the existing amount and unit", async () => {
            var current = await user.Item(path, id);
            await user.Call("PUT", path + "/items/" + id, new { text = "Зелени ябълки", version = Version(current) });
            current = await user.Item(path, id);
            Check(current["quantity"]!.GetValue<decimal>() == 1.5m && current["unit"]!.GetValue<string>() == "kg", "Old edit does not reset amount.");
        });
        await test("Changing a purchased quantity reactivates the list and clears the purchase", async () => {
            var current = await user.Item(path, id);
            await user.Call("PUT", path + "/items/" + id + "/purchase", new { purchased = true, version = Version(current) });
            Check((await user.Call("GET", path))!["completed"]!.GetValue<bool>(), "All purchased completes list.");
            current = await user.Item(path, id);
            await user.Call("PUT", path + "/items/" + id, new { text = "Зелени ябълки", quantity = 2m, unit = "kg", version = Version(current) });
            var state = (await user.Call("GET", path))!;
            current = await user.Item(path, id);
            Check(!state["completed"]!.GetValue<bool>() && state["completedAt"] == null && !current["purchased"]!.GetValue<bool>(), "Changed amount needs purchasing again.");
            Check(current["purchasedBy"] == null && current["purchasedAt"] == null, "Purchase metadata cleared.");
        });
        await test("Saving identical product details does not undo a purchase or create a phantom change", async () => {
            var current = await user.Item(path, id);
            await user.Call("PUT", path + "/items/" + id + "/purchase", new { purchased = true, version = Version(current) });
            current = await user.Item(path, id);
            var pending = (await user.Call("GET", path + "/summary"))!["count"]!.GetValue<int>();
            await user.Call("PUT", path + "/items/" + id, new { text = "Зелени ябълки", quantity = 2m, unit = "kg", version = Version(current) });
            var unchanged = await user.Item(path, id);
            Check(Version(unchanged) == Version(current) && unchanged["purchased"]!.GetValue<bool>(), "No-op retains purchase and version.");
            Check((await user.Call("GET", path + "/summary"))!["count"]!.GetValue<int>() == pending, "No-op adds no summary activity.");
        });
        await test("Duplicate product names warn even with different quantities and allow an explicit override", async () => {
            await user.Call("POST", path + "/items", new { text = "зелени ябълки", quantity = 3m, unit = "kg" }, 409);
            await user.Call("POST", path + "/items", new { text = "зелени ябълки", quantity = 3m, unit = "kg", allowDuplicate = true });
            Check((await user.Call("GET", path))!["items"]!.AsArray().Count == 2, "Explicit duplicate remains a separate row.");
        });
        await test("Deleting the last products ends shopping and a later start sends a fresh alert", async () => {
            await user.Call("POST", path + "/shopping/start");
            var items = (await user.Call("GET", path))!["items"]!.AsArray();
            foreach (var item in items) await user.Call("DELETE", path + "/items/" + item!["id"]!.GetValue<string>() + "?version=" + Version(item));
            var empty = (await user.Call("GET", path))!;
            Check(empty["shoppers"]!.AsArray().Count == 0 && !empty["completed"]!.GetValue<bool>(), "Empty lists cannot retain active shoppers.");
            await user.Call("POST", path + "/items", new { text = "Хляб", quantity = 2, unit = "pcs" });
            await user.Call("POST", path + "/shopping/start");
            var notices = (await user.Call("GET", "/notifications/"))!.AsArray();
            Check(notices.Count(n => n!["kind"]!.GetValue<string>() == "started") == 2, "New run has a fresh start alert.");
        });
        await test("Friendly notifications keep sender metadata separate from human-readable activity", async () => {
            await user.Call("POST", path + "/notify");
            var notice = (await user.Call("GET", "/notifications/"))!.AsArray().First(n => n!["kind"]!.GetValue<string>() == "summary")!;
            Check(notice["title"]!.GetValue<string>() == "Промени в „За дома“", "Friendly title.");
            Check(notice["senderName"]!.GetValue<string>() == "Мария" && notice["text"]!.GetValue<string>().Contains("Мария добави „Хляб · 2 бр.“"), "Readable action and quantity.");
            Check(notice["listAvailable"]!.GetValue<bool>(), "Active list can be opened.");
        });
        await test("A notice for a deleted list remains readable without offering a broken list link", async () => {
            await user.Call("DELETE", path);
            var notice = (await user.Call("GET", "/notifications/"))!.AsArray().First(n => n!["kind"]!.GetValue<string>() == "summary")!;
            Check(!notice["listAvailable"]!.GetValue<bool>(), "Deleted list marked unavailable.");
            await user.Call("PUT", "/notifications/" + notice["id"]!.GetValue<long>() + "/read");
        });
    }
    static string Version(JsonNode item) => item["version"]!.GetValue<string>();
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); }
}
