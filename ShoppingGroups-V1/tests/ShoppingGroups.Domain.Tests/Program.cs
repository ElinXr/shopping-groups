using ShoppingGroups.Domain;
using System.Data.Common;

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
Console.WriteLine($"{passed} domain tests passed.");
