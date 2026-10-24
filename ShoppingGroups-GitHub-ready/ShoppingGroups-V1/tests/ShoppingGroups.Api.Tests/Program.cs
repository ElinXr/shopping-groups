using System.Net;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

var baseUrl = Environment.GetEnvironmentVariable("TEST_BASE_URL") ?? "http://localhost:8080";
var mailUrl = Environment.GetEnvironmentVariable("TEST_MAIL_URL") ?? "http://localhost:8025";
var passed = 0;
void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
async Task Test(string name, Func<Task> work)
{
    await work(); passed++; Console.WriteLine($"PASS {name}");
}
try
{
    using var owner = new Client(baseUrl);
    using var member = new Client(baseUrl);
    using var stranger = new Client(baseUrl);
    using var admin = new Client(baseUrl);
    for (var attempt = 0; ; attempt++)
    {
        try { var r = await owner.Http.GetAsync("/health/ready"); if (r.IsSuccessStatusCode) break; }
        catch (HttpRequestException) { }
        if (attempt > 89) throw new Exception("API did not become ready within 90 seconds.");
        await Task.Delay(1000);
    }
    await Test("Anonymous requests cannot read private groups", async () => await stranger.Call("GET", "/groups", expected: 401));
    await Test("Mutating requests without CSRF fail", async () =>
    {
        var r = await stranger.Http.PostAsJsonAsync("/api/auth/login", new { email = "nobody@example.com", password = "Invalid_123!" });
        Assert((int)r.StatusCode == 400, "Missing CSRF must fail before credential processing.");
    });
    var suffix = Guid.NewGuid().ToString("N")[..10];
    var ownerEmail = $"owner-{suffix}@tests.example";
    var memberEmail = $"member-{suffix}@tests.example";
    var strangerEmail = $"stranger-{suffix}@tests.example";
    const string password = "Integration_123!";
    await Test("Public registration and cookie login work for three independent users", async () =>
    {
        await owner.Register(ownerEmail, password, "Owner");
        await member.Register(memberEmail, password, "Member");
        await stranger.Register(strangerEmail, password, "Stranger");
    });
    var ownerMe = (await owner.Call("GET", "/auth/me"))!;
    var memberMe = (await member.Call("GET", "/auth/me"))!;
    var memberId = memberMe["id"]!.GetValue<string>();
    var secondaryUrl = Environment.GetEnvironmentVariable("TEST_SECONDARY_API_URL");
    if (!string.IsNullOrEmpty(secondaryUrl))
    {
        await Test("Database key ring decrypts a login cookie in a second independent API process", async () =>
        {
            using var secondary = new HttpClient { BaseAddress = new Uri(secondaryUrl), Timeout = TimeSpan.FromSeconds(20) };
            for (var attempt = 0; ; attempt++)
            {
                try { if ((await secondary.GetAsync("/health/ready")).IsSuccessStatusCode) break; }
                catch (HttpRequestException) { }
                if (attempt > 89) throw new Exception("Secondary key-persistence API did not become ready.");
                await Task.Delay(1000);
            }
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
            request.Headers.Add("Cookie", owner.Cookies.GetCookieHeader(new Uri(baseUrl)));
            using var response = await secondary.SendAsync(request);
            Assert(response.IsSuccessStatusCode, "A replacement process must be able to decrypt the existing login cookie using database keys.");
            var restored = await response.Content.ReadFromJsonAsync<JsonObject>();
            Assert(restored!["id"]!.GetValue<string>() == ownerMe["id"]!.GetValue<string>(), "The restored session must identify the same user.");
        });
    }
    await Test("Public accounts cannot assign themselves admin", async () =>
    {
        Assert(!ownerMe["isAdmin"]!.GetValue<bool>(), "Public account is not an admin.");
        await owner.Call("GET", "/admin/overview", expected: 403);
    });
    var group = (await owner.Call("POST", "/groups", new { name = "Integration household" }))!["id"]!.GetValue<string>();
    var root = $"/groups/{group}";
    await Test("Strangers cannot enumerate group members or lists", async () =>
    {
        await stranger.Call("GET", root, expected: 404); await stranger.Call("GET", root + "/lists/", expected: 404);
        Assert((await stranger.Call("GET", "/groups"))!.AsArray().Count == 0, "Private groups must not appear in search.");
    });
    string invitation = "";
    await Test("Owner creates single-use link invitation", async () =>
    {
        var result = (await owner.Call("POST", root + "/invitations", new { maxUses = 1, expiresInDays = 1 }))!;
        invitation = new Uri(result["url"]!.GetValue<string>()).Query.Split("invite=")[1];
        Assert(invitation.Length == 64, "Strong opaque invitation token.");
        var saved = (await owner.Call("GET", root + "/invitations"))!.AsArray()[0]!;
        Assert(saved["tokenHash"] == null && saved["token"] == null, "Invitation secrets must not be listed.");
    });
    await Test("Invitation admits a new member and enforces maximum uses", async () =>
    {
        await member.Call("POST", "/invitations/accept", new { token = invitation });
        await stranger.Call("POST", "/invitations/accept", new { token = invitation }, 400);
        Assert((await member.Call("GET", root))!["members"]!.AsArray().Count == 2, "Both members must be visible.");
    });
    await Test("Regular members cannot issue invites or rename a group", async () =>
    {
        await member.Call("POST", root + "/invitations", new { maxUses = 5, expiresInDays = 7 }, 403);
        await member.Call("PUT", root, new { name = "Hijacked" }, 403);
    });
    await Test("Revoked invitations fail", async () =>
    {
        var result = (await owner.Call("POST", root + "/invitations", new { maxUses = 2, expiresInDays = 1 }))!;
        var token = new Uri(result["url"]!.GetValue<string>()).Query.Split("invite=")[1];
        await owner.Call("DELETE", root + "/invitations/" + result["id"]!.GetValue<string>());
        await stranger.Call("POST", "/invitations/accept", new { token }, 400);
    });
    await Test("Email invitations require the verified matching address", async () =>
    {
        var result = (await owner.Call("POST", root + "/invitations", new { email = strangerEmail, maxUses = 50, expiresInDays = 1 }))!;
        var token = new Uri(result["url"]!.GetValue<string>()).Query.Split("invite=")[1];
        await member.Call("POST", "/invitations/accept", new { token }, 403);
        await stranger.Call("POST", "/invitations/accept", new { token }, 403);
        var confirmation = await FindMail(mailUrl, strangerEmail, "confirm");
        await stranger.Call("POST", "/auth/confirm", new { email = strangerEmail, token = confirmation });
        await stranger.Call("POST", "/invitations/accept", new { token });
        await owner.Call("DELETE", root + "/members/" + (await stranger.Call("GET", "/auth/me"))!["id"]!.GetValue<string>());
    });
    var list = (await member.Call("POST", root + "/lists/", new { name = "Weekly list" }))!["id"]!.GetValue<string>();
    var listRoot = root + "/lists/" + list;
    var secondList = (await owner.Call("POST", root + "/lists/", new { name = "Home supplies" }))!["id"]!.GetValue<string>();
    await Test("Multiple lists coexist and empty lists are active", async () =>
    {
        Assert((await member.Call("GET", root + "/lists/"))!.AsArray().Count == 2, "Group supports multiple lists.");
        Assert(!(await owner.Call("GET", listRoot))!["completed"]!.GetValue<bool>(), "An empty list is active.");
        await owner.Call("POST", listRoot + "/shopping/start", expected: 400);
    });
    using var socket = new ClientWebSocket();
    socket.Options.Cookies = member.Cookies;
    using var socketTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
    var hubUri = new Uri(baseUrl.Replace("http://", "ws://").Replace("https://", "wss://") + "/hubs/events");
    await Test("SignalR accepts authenticated JSON protocol handshake", async () =>
    {
        await socket.ConnectAsync(hubUri, socketTimeout.Token);
        await socket.SendAsync(Encoding.UTF8.GetBytes("{\"protocol\":\"json\",\"version\":1}\u001e"), WebSocketMessageType.Text, true, socketTimeout.Token);
        var data = new byte[8192];
        var frame = await socket.ReceiveAsync(data.AsMemory(), socketTimeout.Token);
        Assert(Encoding.UTF8.GetString(data, 0, frame.Count).Contains("{}\u001e"), "Handshake acknowledgment must be received.");
    });
    var product = (await owner.Call("POST", listRoot + "/items", new { text = "Прясно мляко 2 л" }))!["id"]!.GetValue<string>();
    await Test("Group writes deliver a realtime invalidation to the other member", async () =>
    {
        var data = new byte[8192];
        while (true)
        {
            var frame = await socket.ReceiveAsync(data.AsMemory(), socketTimeout.Token);
            var json = Encoding.UTF8.GetString(data, 0, frame.Count);
            if (json.Contains("Changed")) { Assert(json.Contains(group), "Event must identify the changed group."); break; }
        }
    });
    await Test("Duplicates warn but explicit override permits them", async () =>
    {
        var r = await member.Call("POST", listRoot + "/items", new { text = " ПРЯСНО  МЛЯКО 2 Л " }, 409);
        Assert(r!["code"]!.GetValue<string>() == "duplicate", "Duplicate warning must be distinguishable from write conflicts.");
        await member.Call("POST", listRoot + "/items", new { text = "прясно мляко 2 л", allowDuplicate = true });
    });
    var item = await owner.Item(listRoot, product);
    await Test("Any member can edit another member's product", async () => await member.Call("PUT", listRoot + "/items/" + product, new { text = "Мляко 3 л", version = item["version"]!.GetValue<string>() }));
    await Test("Stale product versions are rejected", async () => await owner.Call("PUT", listRoot + "/items/" + product + "/purchase", new { purchased = true, version = item["version"]!.GetValue<string>() }, 409));
    await Test("Two participants can shop simultaneously without duplicate start alerts", async () =>
    {
        await owner.Call("POST", listRoot + "/shopping/start"); await member.Call("POST", listRoot + "/shopping/start");
        await member.Call("POST", listRoot + "/shopping/start");
        Assert((await owner.Call("GET", listRoot))!["shoppers"]!.AsArray().Count == 2, "Two active shoppers, no duplicate own session.");
        var notifications = (await member.Call("GET", "/notifications/"))!.AsArray();
        Assert(notifications.Count(x => x!["kind"]!.GetValue<string>() == "started") == 1, "One automatic start notification per run.");
    });
    await Test("Manual notification previews changes and consumes them once", async () =>
    {
        Assert((await member.Call("GET", listRoot + "/summary"))!["count"]!.GetValue<int>() == 3, "Two additions and one edit.");
        await member.Call("POST", listRoot + "/notify");
        Assert((await owner.Call("GET", listRoot + "/summary"))!["count"]!.GetValue<int>() == 0, "Sent changes no longer pending.");
        await owner.Call("POST", listRoot + "/notify", expected: 400);
    });
    await Test("Concurrent writes cannot silently overwrite each other", async () =>
    {
        var current = await owner.Item(listRoot, product);
        var version = current["version"]!.GetValue<string>();
        var results = await Task.WhenAll(owner.Raw("PUT", listRoot + "/items/" + product, new { text = "Мляко A", version }), member.Raw("PUT", listRoot + "/items/" + product, new { text = "Мляко B", version }));
        Assert(results.Count(r => r.IsSuccessStatusCode) == 1 && results.Count(r => (int)r.StatusCode == 409) == 1, "Exactly one write succeeds and one conflicts.");
    });
    await Test("Purchasing all items completes the list, stops sessions and notifies everyone once", async () =>
    {
        foreach (var p in (await owner.Call("GET", listRoot))!["items"]!.AsArray())
            await member.Call("PUT", listRoot + "/items/" + p!["id"]!.GetValue<string>() + "/purchase", new { purchased = true, version = p["version"]!.GetValue<string>() });
        var l = (await owner.Call("GET", listRoot))!;
        Assert(l["completed"]!.GetValue<bool>() && l["shoppers"]!.AsArray().Count == 0, "Completion closes all active sessions.");
        foreach (var client in new[] { owner, member }) Assert((await client.Call("GET", "/notifications/"))!.AsArray().Count(n => n!["kind"]!.GetValue<string>() == "completed") == 1, "Each member has exactly one completion alert.");
        Assert((await owner.Call("GET", root + "/lists/"))!.AsArray().Count == 2, "Completed list remains visible.");
    });
    await Test("Adding a product to completed list reactivates it", async () =>
    {
        await member.Call("POST", listRoot + "/items", new { text = "Сапун" });
        Assert(!(await owner.Call("GET", listRoot))!["completed"]!.GetValue<bool>(), "New product reactivates list.");
    });
    await Test("Any member can delete a product added by another", async () =>
    {
        var current = await member.Item(listRoot, product);
        await member.Call("DELETE", listRoot + "/items/" + product + "?version=" + current["version"]!.GetValue<string>());
        Assert((await owner.Call("GET", listRoot))!["items"]!.AsArray().All(p => p!["id"]!.GetValue<string>() != product), "Deleted product must disappear.");
    });
    await Test("Archiving is personal, not a group-wide removal", async () =>
    {
        await owner.Call("PUT", listRoot + "/archive", new { archived = true });
        Assert((await owner.Call("GET", root + "/lists/"))!.AsArray().Count == 1, "Owner hides archived list.");
        Assert((await member.Call("GET", root + "/lists/"))!.AsArray().Count == 2, "Other member still sees list.");
        Assert((await owner.Call("GET", root + "/lists/?view=archived"))!.AsArray().Count == 1, "Owner can view archive.");
        await owner.Call("PUT", listRoot + "/archive", new { archived = false });
    });
    await Test("List deletion hides content and restore preserves products", async () =>
    {
        var count = (await owner.Call("GET", listRoot))!["items"]!.AsArray().Count;
        await member.Call("DELETE", listRoot);
        await owner.Call("GET", listRoot, expected: 404);
        Assert((await owner.Call("GET", root + "/lists/?view=trash"))!.AsArray().Count == 1, "Deleted list is in trash.");
        await owner.Call("POST", listRoot + "/restore");
        Assert((await member.Call("GET", listRoot))!["items"]!.AsArray().Count == count, "Restoration preserves products.");
    });
    await Test("Cross-group product and list IDs cannot bypass access checks", async () =>
    {
        var other = (await stranger.Call("POST", "/groups", new { name = "Other group" }))!["id"]!.GetValue<string>();
        await stranger.Call("GET", $"/groups/{other}/lists/{list}", expected: 404);
        await stranger.Call("POST", listRoot + "/items", new { text = "Intrusion" }, 404);
    });
    await Test("Activity log includes actor and member edits", async () =>
    {
        var log = (await owner.Call("GET", root + "/activity"))!.AsArray();
        Assert(log.Any(x => x!["kind"]!.GetValue<string>() == "item.edited" && x["actorName"]!.GetValue<string>() == "Member"), "Member edits appear in activity.");
    });
    await Test("Notification read receipts are per user", async () =>
    {
        var notice = (await owner.Call("GET", "/notifications/"))!.AsArray()[0]!;
        await member.Call("PUT", "/notifications/" + notice["id"]!.GetValue<long>() + "/read", expected: 404);
        await owner.Call("PUT", "/notifications/" + notice["id"]!.GetValue<long>() + "/read");
    });
    await Test("Only administrator can moderate groups and every action is audited", async () =>
    {
        await admin.Login("admin@tests.example", "Integration_Admin_123!");
        await admin.Call("GET", "/admin/overview");
        await admin.Call("PUT", $"/admin/groups/{group}/disabled", new { disabled = true });
        await member.Call("GET", listRoot, expected: 403);
        await admin.Call("PUT", $"/admin/groups/{group}/disabled", new { disabled = false });
        Assert((await admin.Call("GET", "/admin/audit"))!.AsArray().Count >= 2, "Moderation actions are recorded.");
    });
    await Test("Removing a member immediately revokes HTTP access", async () =>
    {
        await owner.Call("DELETE", root + "/members/" + memberId);
        await member.Call("GET", listRoot, expected: 404);
        await member.Call("GET", root + "/activity", expected: 404);
    });
    await Test("Confirmed user can reset password; old session is revoked", async () =>
    {
        await stranger.Call("POST", "/auth/forgot-password", new { email = strangerEmail });
        var token = await FindMail(mailUrl, strangerEmail, "reset");
        using var resetClient = new Client(baseUrl);
        await resetClient.Call("POST", "/auth/reset-password", new { email = strangerEmail, token, password = "Changed_Password_123!" });
        await stranger.Call("GET", "/auth/me", expected: 401);
        await stranger.Login(strangerEmail, "Changed_Password_123!");
    });
    await Test("Disabled users lose existing sessions immediately", async () =>
    {
        await admin.Call("PUT", $"/admin/users/{memberId}/disabled", new { disabled = true });
        await member.Call("GET", "/auth/me", expected: 401);
        await member.Call("POST", "/auth/login", new { email = memberEmail, password }, 401);
    });
    await Test("Logout invalidates the browser session", async () =>
    {
        await owner.Call("POST", "/auth/logout");
        await owner.Call("GET", "/auth/me", expected: 401);
    });
    Console.WriteLine($"{passed} PostgreSQL/HTTP/SignalR integration tests passed.");
    return 0;
}
catch (Exception ex) { Console.Error.WriteLine($"FAIL after {passed} tests: {ex}"); return 1; }

static async Task<string> FindMail(string mailUrl, string recipient, string action)
{
    using var http = new HttpClient { BaseAddress = new Uri(mailUrl) };
    for (var attempt = 0; attempt < 45; attempt++)
    {
        var inbox = await http.GetFromJsonAsync<JsonObject>("/api/v1/messages");
        foreach (var message in inbox!["messages"]!.AsArray())
        {
            if (!message!["To"]!.AsArray().Any(to => to!["Address"]!.GetValue<string>() == recipient)) continue;
            var detail = await http.GetFromJsonAsync<JsonObject>("/api/v1/message/" + message["ID"]!.GetValue<string>());
            var body = detail!["Text"]!.GetValue<string>();
            var match = Regex.Match(body, $@"[?&]{action}=([^&\s]+)");
            if (match.Success) return Uri.UnescapeDataString(match.Groups[1].Value);
        }
        await Task.Delay(1000);
    }
    throw new Exception($"Expected {action} email was not delivered to Mailpit.");
}

sealed class Client : IDisposable
{
    public CookieContainer Cookies { get; } = new();
    public HttpClient Http { get; }
    string csrf = "";
    public Client(string baseUrl) => Http = new HttpClient(new HttpClientHandler { CookieContainer = Cookies, AllowAutoRedirect = false }) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(20) };
    public async Task Register(string email, string password, string displayName) { await Call("POST", "/auth/register", new { email, password, displayName }); await Login(email, password); }
    public async Task Login(string email, string password) { csrf = ""; await Call("POST", "/auth/login", new { email, password }); csrf = ""; }
    public async Task<HttpResponseMessage> Raw(string method, string path, object? body = null)
    {
        if (method != "GET" && csrf.Length == 0)
        {
            var token = await Http.GetFromJsonAsync<JsonObject>("/api/security/csrf");
            csrf = token!["token"]!.GetValue<string>();
        }
        var request = new HttpRequestMessage(new HttpMethod(method), "/api" + path);
        if (method != "GET") request.Headers.Add("X-CSRF-TOKEN", csrf);
        if (body != null) request.Content = JsonContent.Create(body);
        return await Http.SendAsync(request);
    }
    public async Task<JsonNode?> Call(string method, string path, object? body = null, int expected = 200)
    {
        using var response = await Raw(method, path, body);
        var text = await response.Content.ReadAsStringAsync();
        if (response.StatusCode == HttpStatusCode.Unauthorized) csrf = "";
        if ((int)response.StatusCode != expected && !(expected == 200 && response.StatusCode == HttpStatusCode.NoContent))
            throw new Exception($"{method} {path}: expected {expected}, got {(int)response.StatusCode}: {text}");
        return text.Length == 0 ? null : JsonNode.Parse(text);
    }
    public async Task<JsonNode> Item(string listRoot, string id) => (await Call("GET", listRoot))!["items"]!.AsArray().Single(x => x!["id"]!.GetValue<string>() == id)!;
    public void Dispose() => Http.Dispose();
}
