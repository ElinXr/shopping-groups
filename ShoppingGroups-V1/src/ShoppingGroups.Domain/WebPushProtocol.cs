using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ShoppingGroups.Domain;

// RFC 8292 authentication for RFC 8030 empty-body push messages.
// No private shopping content is sent to the push provider or lock screen.
public static class WebPushProtocol
{
    public static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    public static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));

    public static (string PublicKey, string PrivateKey) GenerateKeys()
    {
        using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = key.ExportParameters(false);
        return (Encode([4, .. p.Q.X!, .. p.Q.Y!]), Convert.ToBase64String(key.ExportPkcs8PrivateKey()));
    }

    public static bool AllowedEndpoint(string endpoint, string? testOrigin = null)
    {
        if (endpoint.Length > 4096 || !Uri.TryCreate(endpoint, UriKind.Absolute, out var uri)
            || uri.UserInfo.Length != 0 || uri.Fragment.Length != 0 || uri.AbsolutePath == "/") return false;
        if (testOrigin != null && uri.GetLeftPart(UriPartial.Authority) == testOrigin) return true;
        if (uri.Scheme != "https" || !uri.IsDefaultPort) return false;
        var host = uri.IdnHost.ToLowerInvariant();
        return host == "fcm.googleapis.com"
            || host == "updates.push.services.mozilla.com" || host.EndsWith(".push.services.mozilla.com", StringComparison.Ordinal)
            || host == "web.push.apple.com" || host.EndsWith(".push.apple.com", StringComparison.Ordinal)
            || host.EndsWith(".notify.windows.com", StringComparison.Ordinal);
    }

    public static string Authorization(string endpoint, string publicKey, string privateKey, string subject, DateTimeOffset now)
    {
        var contact = new Uri(subject, UriKind.Absolute);
        if (contact.Scheme is not ("https" or "mailto")) throw new ArgumentException("Push subject must be an HTTPS or mailto URL.");
        using var key = ECDsa.Create();
        key.ImportPkcs8PrivateKey(Convert.FromBase64String(privateKey), out _);
        var p = key.ExportParameters(false);
        if (publicKey != Encode([4, .. p.Q.X!, .. p.Q.Y!])) throw new ArgumentException("Push keys do not match.");
        var header = Encode(Encoding.UTF8.GetBytes("""{"typ":"JWT","alg":"ES256"}"""));
        var claims = Encode(JsonSerializer.SerializeToUtf8Bytes(new {
            aud = new Uri(endpoint).GetLeftPart(UriPartial.Authority),
            exp = now.AddHours(12).ToUnixTimeSeconds(),
            sub = subject
        }));
        var message = $"{header}.{claims}";
        var signature = key.SignData(Encoding.ASCII.GetBytes(message), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
        return $"vapid t={message}.{Encode(signature)}, k={publicKey}";
    }

    public static HttpRequestMessage Request(string endpoint, string authorization)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent([]) };
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("TTL", "86400");
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Topic", "shopping-groups");
        return request;
    }
}
