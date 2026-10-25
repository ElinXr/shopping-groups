using ShoppingGroups.Domain;
using System.Data.Common;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

var passed = 0;
void Test(string name, Action action)
{
    try { action(); Console.WriteLine($"PASS {name}"); passed++; }
    catch (Exception ex) { Console.Error.WriteLine($"FAIL {name}: {ex.Message}"); Environment.Exit(1); }
}
void Equal<T>(T expected, T actual) { if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}"); }
void Reject(string code, Action action)
{
    try { action(); } catch (RuleException ex) { Equal(code, ex.Code); return; }
    throw new Exception($"Expected {code} rejection");
}
Test("Trim free text while preserving internal product details", () => Equal("мляко 2 л", Rules.Text("  мляко 2 л  ", "Продукт")));
Test("Reject empty text", () => Reject("validation", () => Rules.Text(" \t\n", "Продукт")));
Test("Reject null text", () => Reject("validation", () => Rules.Text(null, "Продукт")));
Test("Allow maximum product length", () => Equal(200, Rules.Text(new string('я', 200), "Продукт").Length));
Test("Reject overlong product", () => Reject("validation", () => Rules.Text(new string('я', 201), "Продукт")));
Test("Respect group and list name limits", () => Reject("validation", () => Rules.Text(new string('a', 101), "Група", 100)));
Test("Duplicate key ignores case and repeated Unicode whitespace", () => Equal(Rules.Normalize("  прясно\tмляко "), Rules.Normalize("ПРЯСНО  МЛЯКО")));
Test("Duplicate key normalizes compatibility characters", () => Equal(Rules.Normalize("ＡＢＣ"), Rules.Normalize("abc")));
Test("Duplicate key preserves distinct quantities", () => Equal(false, Rules.Normalize("мляко 1л") == Rules.Normalize("мляко 2л")));
Test("Indexed duplicate keys preserve canonical equality", () => Equal(Rules.DuplicateKey("МЛЯКО  2 Л"), Rules.DuplicateKey(" мляко 2 л ")));
Test("Indexed duplicate keys keep different quantities distinct", () => Equal(false, Rules.DuplicateKey("1 л") == Rules.DuplicateKey("2 л")));
Test("Indexed duplicate keys stay bounded for expanding Unicode", () => Equal(64, Rules.DuplicateKey(new string('\uFDFA', 200)).Length));
Test("Empty list is never completed", () => Equal(false, Rules.Completed([])));
Test("Single unpurchased item keeps list active", () => Equal(false, Rules.Completed([false])));
Test("All purchased completes list", () => Equal(true, Rules.Completed([true, true, true])));
Test("Adding item reactivates a completed list", () => Equal(false, Rules.Completed([true, true, false])));
Test("Unpurchase reactivates a completed list", () => Equal(false, Rules.Completed([false, true])));
Test("Deleting last item leaves an empty active list", () => Equal(false, Rules.Completed([])));
var deleted = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
Test("Restore within retention window", () => Equal(true, Rules.CanRestore(deleted, deleted.AddDays(89))));
Test("Restore one tick before expiry", () => Equal(true, Rules.CanRestore(deleted, deleted.AddDays(90).AddTicks(-1))));
Test("Restore denied at exact 90 day boundary", () => Equal(false, Rules.CanRestore(deleted, deleted.AddDays(90))));
Test("Restore denied after expiry", () => Equal(false, Rules.CanRestore(deleted, deleted.AddDays(100))));
Test("Active lists cannot be restored", () => Equal(false, Rules.CanRestore(null, deleted)));
Test("Matching version accepts update", () => { var version = Guid.NewGuid(); Rules.CheckVersion(version, version); });
Test("Stale update is rejected", () => Reject("conflict", () => Rules.CheckVersion(Guid.NewGuid(), Guid.NewGuid())));
Test("Empty summary is rejected", () => Reject("no_changes", () => Rules.Summary([])));
Test("Non-product activity is excluded from manual summary", () => Reject("no_changes", () => Rules.Summary([("shopping.started", "started"), ("summary.sent", "sent")])));
Test("Summary combines edits and purchases with meaningful counts", () =>
{
    var summary = Rules.Summary([("item.added", "Добавено: мляко"), ("item.added", "Добавено: хляб"), ("item.purchased", "Купено: мляко"), ("member.joined", "joined")]);
    Equal(true, summary.Contains("Добавени: 2")); Equal(true, summary.Contains("Купени: 1")); Equal(false, summary.Contains("joined"));
});
Test("Large summary contains counts for all changes but only 20 latest details", () =>
{
    var summary = Rules.Summary(Enumerable.Range(0, 30).Select(i => ("item.edited", $"edit-{i}")));
    Equal(true, summary.StartsWith("Променени: 30")); Equal(21, summary.Split('\n').Length); Equal(false, summary.Contains("edit-0\n")); Equal(true, summary.EndsWith("edit-29"));
});
DbConnectionStringBuilder Connection(string url) => new() { ConnectionString = PostgresConnection.Resolve(null, url) };
void InvalidConnection(string? url)
{
    try { PostgresConnection.Resolve(null, url); }
    catch (FormatException ex) { Equal(false, ex.Message.Contains("secret-password", StringComparison.Ordinal)); return; }
    throw new Exception("Invalid database URL was accepted");
}
Test("Native Npgsql connection settings take precedence over DATABASE_URL", () => Equal("Host=local;Password=private;Database=test", PostgresConnection.Resolve("Host=local;Password=private;Database=test", "postgres://user:other@host/db")));
Test("Neon unpooled URL takes precedence over its pooled URL", () => {
    var c = new DbConnectionStringBuilder { ConnectionString = PostgresConnection.Resolve(null, "postgres://u:p@ep-example-pooler.host/db", "postgres://u:p@ep-example.host/db") };
    Equal("ep-example.host", c["Host"]);
});
Test("Native Npgsql settings take precedence over Neon unpooled URL", () => Equal("Host=local;Database=test", PostgresConnection.Resolve("Host=local;Database=test", "postgres://u:p@pooled/db", "postgres://u:p@direct/db")));
Test("Empty unpooled URL falls back to DATABASE_URL", () => Equal(PostgresConnection.Resolve(null, "postgres://u:p@host/db"), PostgresConnection.Resolve(null, "postgres://u:p@host/db", "  ")));
Test("Neon URL supplies host, user, password and database", () => {
    var c = Connection("postgresql://neondb_owner:secret@ep-example.eu-central-1.aws.neon.tech/neondb");
    Equal("ep-example.eu-central-1.aws.neon.tech", c["Host"]); Equal("neondb_owner", c["Username"]); Equal("secret", c["Password"]); Equal("neondb", c["Database"]);
});
Test("Database URLs default to PostgreSQL port and certificate validation", () => { var c = Connection("postgres://u:p@host/db"); Equal("5432", c["Port"]); Equal("VerifyFull", c["SSL Mode"]); });
Test("Custom database URL port is retained", () => Equal("5433", Connection("postgres://u:p@host:5433/db")["Port"]));
Test("Encoded credentials round-trip without connection-string injection", () => {
    var c = Connection("postgres://user:p%3BPassword%3Dattack%3B%22%2F%40@host/db");
    Equal("p;Password=attack;\"/@", c["Password"]); Equal("user", c["Username"]); Equal(6, c.Count);
});
Test("Percent-encoded plus and colon remain literal password characters", () => Equal("a+b:c", Connection("postgres://u:a%2Bb%3Ac@host/db")["Password"]));
Test("Encoded database name is decoded once", () => Equal("test database", Connection("postgres://u:p@host/test%20database")["Database"]));
Test("Provider SSL and channel binding URL options are supported", () => { var c = Connection("postgres://u:p@host/db?sslmode=require&channel_binding=require"); Equal("Require", c["SSL Mode"]); Equal("Require", c["Channel Binding"]); });
Test("Explicit verify-full survives URL conversion", () => Equal("VerifyFull", Connection("postgres://u:p@host/db?sslmode=verify-full")["SSL Mode"]));
Test("Connection timeout and application name are retained", () => { var c = Connection("postgres://u:p@host/db?connect_timeout=30&application_name=Shopping%20Groups"); Equal("30", c["Timeout"]); Equal("Shopping Groups", c["Application Name"]); });
Test("Missing database configuration fails clearly", () => InvalidConnection(null));
Test("HTTP URLs cannot masquerade as database connections", () => InvalidConnection("https://u:secret-password@host/db"));
Test("Missing database name is rejected", () => InvalidConnection("postgres://u:secret-password@host/"));
Test("Missing database username is rejected", () => InvalidConnection("postgres://host/db"));
Test("Ambiguous database path is rejected", () => InvalidConnection("postgres://u:secret-password@host/db/extra"));
Test("Database URL fragments are rejected", () => InvalidConnection("postgres://u:secret-password@host/db#ignored"));
Test("Unknown URL options fail instead of silently changing TLS behavior", () => InvalidConnection("postgres://u:secret-password@host/db?unknown=1"));
Test("Unknown SSL settings are rejected", () => InvalidConnection("postgres://u:secret-password@host/db?sslmode=typo"));
Test("Invalid channel binding is rejected", () => InvalidConnection("postgres://u:secret-password@host/db?channel_binding=typo"));
Test("Connection timeout has a bounded positive value", () => { InvalidConnection("postgres://u:secret-password@host/db?connect_timeout=0"); InvalidConnection("postgres://u:secret-password@host/db?connect_timeout=301"); });
Test("Web Push produces a matching P-256 application key pair", () => {
    var pair = WebPushProtocol.GenerateKeys();
    var bytes = WebPushProtocol.Decode(pair.PublicKey);
    Equal(65, bytes.Length); Equal((byte)4, bytes[0]);
    using var key = ECDsa.Create(); key.ImportPkcs8PrivateKey(Convert.FromBase64String(pair.PrivateKey), out _);
    var point = key.ExportParameters(false);
    Equal(Convert.ToHexString(point.Q.X!), Convert.ToHexString(bytes[1..33]));
    Equal(Convert.ToHexString(point.Q.Y!), Convert.ToHexString(bytes[33..]));
});
Test("VAPID authenticates the provider origin with a valid ES256 JWT and bounded expiry", () => {
    var pair = WebPushProtocol.GenerateKeys();
    var now = DateTimeOffset.FromUnixTimeSeconds(1800000000);
    var authorization = WebPushProtocol.Authorization("https://fcm.googleapis.com/push/private-device", pair.PublicKey, pair.PrivateKey, "https://shopping.example", now);
    var jwt = authorization["vapid t=".Length..].Split(", k=")[0];
    var parts = jwt.Split('.');
    using var header = JsonDocument.Parse(WebPushProtocol.Decode(parts[0]));
    using var claims = JsonDocument.Parse(WebPushProtocol.Decode(parts[1]));
    Equal("ES256", header.RootElement.GetProperty("alg").GetString());
    Equal("https://fcm.googleapis.com", claims.RootElement.GetProperty("aud").GetString());
    Equal("https://shopping.example", claims.RootElement.GetProperty("sub").GetString());
    Equal(now.AddHours(12).ToUnixTimeSeconds(), claims.RootElement.GetProperty("exp").GetInt64());
    var point = WebPushProtocol.Decode(pair.PublicKey);
    using var verify = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..] } });
    Equal(true, verify.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), WebPushProtocol.Decode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation));
    Equal(false, jwt.Contains("private-device", StringComparison.Ordinal));
});
Test("VAPID rejects mismatched application keys", () => {
    var one = WebPushProtocol.GenerateKeys(); var other = WebPushProtocol.GenerateKeys();
    try { WebPushProtocol.Authorization("https://fcm.googleapis.com/push/a", one.PublicKey, other.PrivateKey, "https://shopping.example", DateTimeOffset.UtcNow); }
    catch (ArgumentException) { return; }
    throw new Exception("Mismatched VAPID keys accepted");
});
Test("VAPID rejects insecure contact URLs", () => {
    var pair = WebPushProtocol.GenerateKeys();
    try { WebPushProtocol.Authorization("https://fcm.googleapis.com/push/a", pair.PublicKey, pair.PrivateKey, "http://shopping.example", DateTimeOffset.UtcNow); }
    catch (ArgumentException) { return; }
    throw new Exception("Invalid VAPID subject accepted");
});
Test("Push accepts browser provider endpoints", () => {
    foreach (var host in new[] { "fcm.googleapis.com", "updates.push.services.mozilla.com", "web.push.apple.com", "a.push.apple.com", "wns2.notify.windows.com" })
        Equal(true, WebPushProtocol.AllowedEndpoint("https://" + host + "/private-token"));
});
Test("Push prevents requests to local hosts, plain HTTP and custom ports", () => {
    foreach (var endpoint in new[] { "https://127.0.0.1/push", "http://fcm.googleapis.com/push", "https://fcm.googleapis.com:8443/push", "https://localhost/push", "https://192.168.1.1/push" })
        Equal(false, WebPushProtocol.AllowedEndpoint(endpoint));
});
Test("Push provider allowlist rejects suffix attacks and embedded credentials", () => {
    foreach (var endpoint in new[] { "https://fcm.googleapis.com.evil.test/push", "https://evilpush.apple.com/push", "https://web.push.apple.com@evil.test/push", "https://u:p@fcm.googleapis.com/push" })
        Equal(false, WebPushProtocol.AllowedEndpoint(endpoint));
});
Test("Push rejects missing paths, fragments and oversized endpoints", () => {
    Equal(false, WebPushProtocol.AllowedEndpoint("https://fcm.googleapis.com/"));
    Equal(false, WebPushProtocol.AllowedEndpoint("https://fcm.googleapis.com/push#fragment"));
    Equal(false, WebPushProtocol.AllowedEndpoint("https://fcm.googleapis.com/" + new string('a', 4096)));
});
Test("Push test origin is rejected unless explicitly supplied", () => {
    Equal(false, WebPushProtocol.AllowedEndpoint("http://tests:8099/push/a"));
    Equal(true, WebPushProtocol.AllowedEndpoint("http://tests:8099/push/a", "http://tests:8099"));
    Equal(false, WebPushProtocol.AllowedEndpoint("http://tests.evil:8099/push/a", "http://tests:8099"));
});
Test("Web Push request contains no shopping content and preserves VAPID and TTL", () => {
    using var request = WebPushProtocol.Request("https://fcm.googleapis.com/push/a", "vapid t=signed-token, k=public-key");
    Equal(HttpMethod.Post, request.Method);
    Equal(0L, request.Content!.Headers.ContentLength);
    Equal("86400", request.Headers.GetValues("TTL").Single());
    Equal("vapid t=signed-token, k=public-key", request.Headers.GetValues("Authorization").Single());
    Equal(false, request.Content.Headers.ContentEncoding.Any());
});
// RFC 8291 Appendix A test vector (https://www.rfc-editor.org/rfc/rfc8291).
Test("Encrypted Web Push exactly matches the RFC 8291 interoperability vector", () => {
    const string serverPublic = "BP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A8";
    const string receiverPublic = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    var q = WebPushProtocol.Decode(serverPublic);
    using var sender = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
        Q = new ECPoint { X = q[1..33], Y = q[33..] }, D = WebPushProtocol.Decode("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw") });
    var encrypted = WebPushProtocol.Encrypt(
        WebPushProtocol.Decode("V2hlbiBJIGdyb3cgdXAsIEkgd2FudCB0byBiZSBhIHdhdGVybWVsb24"),
        receiverPublic, "BTBZMqHH6r4Tts7J_aSIgg", sender, WebPushProtocol.Decode("DGv6ra1nlYgDCS1FRnbzlw"));
    Equal("8pfeW0KbunFT06SuDKoJH9Ql87S1QUrdirN6GcG7sFz1y1sqLgVi1VhjVkHsUoEsbI_0LpXMuGvnzQ", WebPushProtocol.Encode(encrypted[86..]));
    Equal(serverPublic, WebPushProtocol.Encode(encrypted[21..86]));
    Equal("DGv6ra1nlYgDCS1FRnbzlw", WebPushProtocol.Encode(encrypted[..16]));
});
Test("Payload encryption validates browser keys and uses fresh randomness", () => {
    var pair = WebPushProtocol.GenerateKeys();
    var auth = WebPushProtocol.Encode(RandomNumberGenerator.GetBytes(16));
    Equal(true, WebPushProtocol.ValidSubscriptionKeys(pair.PublicKey, auth));
    Equal(false, WebPushProtocol.ValidSubscriptionKeys(pair.PublicKey, "invalid"));
    Equal(false, WebPushProtocol.ValidSubscriptionKeys("not-a-curve-point", auth));
    var one = WebPushProtocol.Encrypt(Encoding.UTF8.GetBytes("Мария · Добавено: Мляко"), pair.PublicKey, auth);
    var two = WebPushProtocol.Encrypt(Encoding.UTF8.GetBytes("Мария · Добавено: Мляко"), pair.PublicKey, auth);
    Equal(false, one.SequenceEqual(two));
});
Test("Detailed push transport sends aes128gcm and remains inside provider payload limits", () => {
    var pair = WebPushProtocol.GenerateKeys();
    var payload = NotificationText.Payload(new string('я', 5000), DateTime.UtcNow);
    var encrypted = WebPushProtocol.Encrypt(payload, pair.PublicKey, WebPushProtocol.Encode(RandomNumberGenerator.GetBytes(16)));
    using var request = WebPushProtocol.Request("https://fcm.googleapis.com/push/test", "vapid test", encrypted);
    Equal("aes128gcm", request.Content!.Headers.ContentEncoding.Single());
    Equal(true, request.Content.Headers.ContentLength <= 4096);
});
Test("Notification summaries retain the original author and action time separately from the notifier", () => {
    var added = new DateTime(2026, 10, 25, 12, 25, 0, DateTimeKind.Utc);
    var summary = NotificationText.Summary([("item.added", "Добавено: Мляко", "Мария", added)]);
    var notice = NotificationText.FromActor("Иван", summary, added.AddMinutes(10));
    Equal(true, notice.Contains("Мария · 25.10.2026 14:25 — Добавено: Мляко"));
    Equal(true, notice.StartsWith("От Иван · 25.10.2026 14:35"));
});
Test("Bulgarian notification times respect winter and summer time", () => {
    Equal("15.01.2026 12:00", NotificationText.Time(new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)));
    Equal("15.07.2026 13:00", NotificationText.Time(new DateTime(2026, 7, 15, 10, 0, 0, DateTimeKind.Utc)));
});
Test("Long Unicode notification text is bounded without breaking JSON or emoji", () => {
    var payload = NotificationText.Payload(string.Concat(Enumerable.Repeat("Мария 🛒 добави мляко в 14:25\n", 500)), DateTime.UtcNow);
    Equal(true, payload.Length <= 3000);
    using var json = JsonDocument.Parse(payload);
    Equal(1, json.RootElement.GetProperty("version").GetInt32());
    Equal(true, json.RootElement.GetProperty("body").GetString()!.Contains("Мария"));
    Equal(false, Encoding.UTF8.GetString(payload).Contains('\uFFFD'));
});
Test("Phone previews prioritize the action author and original time over the later sender header", () => {
    var added = new DateTime(2026, 10, 25, 12, 25, 0, DateTimeKind.Utc);
    var summary = NotificationText.Summary([("item.added", "Добавено: Мляко", "Мария", added)]);
    var text = NotificationText.FromActor("Иван", summary, added.AddMinutes(10));
    using var payload = JsonDocument.Parse(NotificationText.Payload(text, added.AddMinutes(10)));
    Equal(true, payload.RootElement.GetProperty("body").GetString()!.StartsWith("Мария · 25.10.2026 14:25 — Добавено: Мляко"));
});
Console.WriteLine($"{passed} domain tests passed.");
