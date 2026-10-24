using System.Data.Common;
using System.Globalization;

namespace ShoppingGroups.Domain;

public static class PostgresConnection
{
    public static string Resolve(string? configured, string? databaseUrl, string? unpooledDatabaseUrl = null)
    {
        var value = !string.IsNullOrWhiteSpace(configured) ? configured.Trim()
            : !string.IsNullOrWhiteSpace(unpooledDatabaseUrl) ? unpooledDatabaseUrl.Trim() : databaseUrl?.Trim();
        if (string.IsNullOrEmpty(value)) throw Invalid();
        if (!value.Contains("://", StringComparison.Ordinal)) return value;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme is not ("postgres" or "postgresql")
            || string.IsNullOrEmpty(uri.Host) || uri.Fragment.Length != 0) throw Invalid();

        var credentials = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(credentials[0]);
        var path = uri.AbsolutePath.TrimStart('/');
        if (user.Length == 0 || path.Length == 0 || path.Contains('/')) throw Invalid();
        var result = new DbConnectionStringBuilder {
            ["Host"] = uri.Host.Trim('[', ']'), ["Port"] = uri.Port < 0 ? 5432 : uri.Port,
            ["Database"] = Uri.UnescapeDataString(path), ["Username"] = user,
            ["Password"] = credentials.Length == 2 ? Uri.UnescapeDataString(credentials[1]) : "",
            ["SSL Mode"] = "VerifyFull"
        };
        foreach (var part in uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var pair = part.Split('=', 2);
            if (pair.Length != 2) throw Invalid();
            var key = Uri.UnescapeDataString(pair[0]).ToLowerInvariant();
            var setting = Uri.UnescapeDataString(pair[1]);
            switch (key)
            {
                case "sslmode":
                    result["SSL Mode"] = setting.ToLowerInvariant() switch {
                        "disable" => "Disable", "allow" => "Allow", "prefer" => "Prefer", "require" => "Require",
                        "verify-ca" => "VerifyCA", "verify-full" => "VerifyFull", _ => throw Invalid()
                    };
                    break;
                case "channel_binding":
                    result["Channel Binding"] = setting.ToLowerInvariant() switch {
                        "disable" => "Disable", "prefer" => "Prefer", "require" => "Require", _ => throw Invalid()
                    };
                    break;
                case "connect_timeout":
                    if (!int.TryParse(setting, NumberStyles.None, CultureInfo.InvariantCulture, out var timeout) || timeout is < 1 or > 300) throw Invalid();
                    result["Timeout"] = timeout;
                    break;
                case "application_name": result["Application Name"] = setting; break;
                default: throw Invalid();
            }
        }
        return result.ConnectionString;
    }

    // Never include the input: database URLs contain a username and password.
    private static FormatException Invalid() => new("Set a valid Npgsql connection string or PostgreSQL DATABASE_URL. URL options supported: sslmode, channel_binding, connect_timeout, application_name.");
}
