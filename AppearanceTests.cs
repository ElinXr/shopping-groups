using System.Text.Json.Nodes;

// Persisted account preferences are tested through independent cookie sessions.
static class AppearanceTests
{
    public static async Task Run(string baseUrl, Func<string, Func<Task>, Task> test)
    {
        var suffix = Guid.NewGuid().ToString("N");
        var email = $"appearance-{suffix}@example.test";
        const string password = "Appearance-Test-42!";
        using var user = new Client(baseUrl);
        using var other = new Client(baseUrl);
        await user.Register(email, password, "Мария");
        await other.Register($"appearance-other-{suffix}@example.test", password, "Иван");
        var userId = (await user.Call("GET", "/auth/me"))!["id"]!.GetValue<string>();
        var otherId = (await other.Call("GET", "/auth/me"))!["id"]!.GetValue<string>();
        await test("Newly registered accounts default to light appearance", async () => {
            Check((await user.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "light", "Explicit light default.");
        });
        await test("An appearance preference is saved to the authenticated account without ending its session", async () => {
            var result = (await user.Call("PUT", "/auth/preferences", new { theme = "dark", userId }))!;
            Check(result["theme"]!.GetValue<string>() == "dark", "Dark theme acknowledged after saving.");
            Check((await user.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "dark", "Profile returns persisted preference.");
            await user.Call("GET", "/groups");
        });
        await test("Appearance survives logout and a new login on another cookie session", async () => {
            await user.Call("POST", "/auth/logout");
            using var newDevice = new Client(baseUrl);
            await newDevice.Login(email, password);
            Check((await newDevice.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "dark", "A different device reads the database value.");
            await user.Login(email, password);
        });
        await test("Profile edits preserve appearance and another account keeps its own choice", async () => {
            await user.Call("PUT", "/auth/profile", new { displayName = "Мария Нова", emailNotifications = false });
            Check((await user.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "dark", "Name/email options do not reset appearance.");
            Check((await other.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "light", "Other accounts are unaffected.");
        });
        await test("Invalid choices and requests from a stale account cannot overwrite a preference", async () => {
            await user.Call("PUT", "/auth/preferences", new { theme = "system", userId }, 400);
            await other.Call("PUT", "/auth/preferences", new { theme = "dark", userId }, 409);
            using var anonymous = new Client(baseUrl);
            await anonymous.Call("PUT", "/auth/preferences", new { theme = "dark", userId = otherId }, 401);
            Check((await user.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "dark", "Invalid request retained existing preference.");
            Check((await other.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "light", "Wrong-account request did not update the other account.");
            await user.Call("PUT", "/auth/preferences", new { theme = "light", userId });
            Check((await user.Call("GET", "/auth/me"))!["theme"]!.GetValue<string>() == "light", "An explicit later choice can change the preference.");
        });
    }
    static void Check(bool value, string message) { if (!value) throw new Exception(message); }
}
