using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Buffers.Binary;

namespace ShoppingGroups.Domain;

// RFC 8292 authentication and RFC 8291 encrypted Web Push payloads.
// Authenticated encryption keeps detailed notification content confidential in transit.
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

    public static bool ValidSubscriptionKeys(string? publicKey, string? auth)
    {
        try
        {
            if (publicKey == null || auth == null || publicKey.Length > 100 || auth.Length > 32) return false;
            var point = Decode(publicKey);
            if (point.Length != 65 || point[0] != 4 || Decode(auth).Length != 16) return false;
            using var key = ECDiffieHellman.Create(new ECParameters {
                Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..] }
            });
            return true;
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException) { return false; }
    }

    // Optional deterministic inputs are used by RFC conformance tests only.
    public static byte[] Encrypt(byte[] plaintext, string publicKey, string auth, ECDiffieHellman? sender = null, byte[]? salt = null)
    {
        if (plaintext.Length > 3993 || !ValidSubscriptionKeys(publicKey, auth)) throw new ArgumentException("Invalid Web Push payload or subscription keys.");
        using var generated = sender == null ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256) : null;
        var server = sender ?? generated!;
        var serverPoint = server.ExportParameters(false).Q;
        byte[] serverPublic = [4, .. serverPoint.X!, .. serverPoint.Y!];
        var uaPublic = Decode(publicKey);
        using var receiver = ECDiffieHellman.Create(new ECParameters {
            Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = uaPublic[1..33], Y = uaPublic[33..] }
        });
        salt ??= RandomNumberGenerator.GetBytes(16);
        if (salt.Length != 16) throw new ArgumentException("Web Push salt must contain 16 bytes.");
        var shared = server.DeriveRawSecretAgreement(receiver.PublicKey);
        var authSecret = Decode(auth);
        byte[] keyInfo = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. uaPublic, .. serverPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, authSecret, keyInfo);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        try
        {
            var result = new byte[86 + plaintext.Length + 17];
            salt.CopyTo(result, 0);
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(16, 4), 4096);
            result[20] = 65;
            serverPublic.CopyTo(result, 21);
            byte[] padded = [.. plaintext, 2];
            using var aes = new AesGcm(cek, 16);
            aes.Encrypt(nonce, padded, result.AsSpan(86, padded.Length), result.AsSpan(86 + padded.Length, 16));
            return result;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(shared);
            CryptographicOperations.ZeroMemory(authSecret);
            CryptographicOperations.ZeroMemory(ikm);
            CryptographicOperations.ZeroMemory(cek);
            CryptographicOperations.ZeroMemory(nonce);
        }
    }

    public static HttpRequestMessage Request(string endpoint, string authorization, byte[]? encryptedBody = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(encryptedBody ?? []) };
        if (encryptedBody != null)
        {
            request.Content.Headers.ContentType = new("application/octet-stream");
            request.Content.Headers.ContentEncoding.Add("aes128gcm");
        }
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("TTL", "86400");
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Headers.TryAddWithoutValidation("Topic", "shopping-groups");
        return request;
    }
}
