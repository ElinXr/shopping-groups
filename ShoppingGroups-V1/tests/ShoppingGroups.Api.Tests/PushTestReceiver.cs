using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Isolated provider: independently validates VAPID and decrypts RFC 8291 payloads.
sealed class PushTestReceiver : IDisposable
{
    readonly HttpListener listener = new();
    readonly ConcurrentDictionary<string, int> accepted = new();
    readonly ConcurrentDictionary<string, string> messages = new();
    readonly CancellationTokenSource stopped = new();
    readonly ECDiffieHellman browserKey = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
    readonly byte[] browserAuth = RandomNumberGenerator.GetBytes(16);
    readonly Task loop;
    public string Origin { get; }
    public string ExpectedPublicKey { get; set; } = "";
    public PushTestReceiver(string origin)
    {
        Origin = origin;
        listener.Prefixes.Add("http://*:" + new Uri(origin).Port + "/");
        listener.Start();
        loop = Run();
    }
    public object Subscription(string endpoint)
    {
        var q = browserKey.ExportParameters(false).Q;
        return new { endpoint, p256dh = Encode([4, .. q.X!, .. q.Y!]), auth = Encode(browserAuth) };
    }
    public int Count(string path) => accepted.GetValueOrDefault(path);
    public string Body(string path) => messages.GetValueOrDefault(path) ?? "";
    public async Task Wait(string path, int count)
    {
        for (var i = 0; i < 100; i++)
        {
            if (Count(path) >= count) return;
            await Task.Delay(100);
        }
        throw new Exception("Expected encrypted Web Push was not accepted by the isolated receiver.");
    }
    async Task Run()
    {
        while (!stopped.IsCancellationRequested)
        {
            HttpListenerContext context;
            try { context = await listener.GetContextAsync().WaitAsync(stopped.Token); }
            catch (Exception) when (stopped.IsCancellationRequested) { break; }
            try
            {
                var request = context.Request;
                var authorization = request.Headers["Authorization"] ?? "";
                var values = authorization["vapid t=".Length..].Split(", k=");
                var parts = values[0].Split('.');
                var point = Decode(values[1]);
                using var claims = JsonDocument.Parse(Decode(parts[1]));
                using var header = JsonDocument.Parse(Decode(parts[0]));
                using var key = ECDsa.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, Q = new ECPoint { X = point[1..33], Y = point[33..] } });
                if (!authorization.StartsWith("vapid t=", StringComparison.Ordinal) || point.Length != 65 || point[0] != 4
                    || values[1] != ExpectedPublicKey
                    || request.HttpMethod != "POST" || request.ContentLength64 <= 0 || request.ContentLength64 > 4096
                    || request.Headers["Content-Encoding"] != "aes128gcm"
                    || request.Headers["TTL"] != "86400" || header.RootElement.GetProperty("alg").GetString() != "ES256"
                    || claims.RootElement.GetProperty("aud").GetString() != Origin
                    || claims.RootElement.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    || !key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                    throw new Exception("Invalid Web Push request.");
                using var data = new MemoryStream();
                await request.InputStream.CopyToAsync(data);
                using var payload = JsonDocument.Parse(Decrypt(data.ToArray()));
                if (payload.RootElement.GetProperty("version").GetInt32() != 1) throw new Exception("Unexpected payload version.");
                var body = payload.RootElement.GetProperty("body").GetString() ?? throw new Exception("Missing message.");
                var path = request.Url!.AbsolutePath;
                if (path.StartsWith("/gone/", StringComparison.Ordinal)) context.Response.StatusCode = 410;
                else
                {
                    messages[path] = body;
                    accepted.AddOrUpdate(path, 1, (_, count) => count + 1);
                    context.Response.StatusCode = 201;
                }
            }
            catch { context.Response.StatusCode = 400; }
            finally { context.Response.Close(); }
        }
    }
    byte[] Decrypt(byte[] body)
    {
        if (body.Length < 103 || body[20] != 65 || body[21] != 4 || BinaryPrimitives.ReadUInt32BigEndian(body.AsSpan(16, 4)) != 4096)
            throw new Exception("Invalid encryption header.");
        var senderPublic = body[21..86];
        using var sender = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = senderPublic[1..33], Y = senderPublic[33..] } });
        var q = browserKey.ExportParameters(false).Q;
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), 4, .. q.X!, .. q.Y!, .. senderPublic];
        var shared = browserKey.DeriveRawSecretAgreement(sender.PublicKey);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, shared, 32, browserAuth, info);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, body[..16], Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, body[..16], Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));
        var plaintext = new byte[body.Length - 86 - 16];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, body.AsSpan(86, plaintext.Length), body.AsSpan(body.Length - 16), plaintext);
        if (plaintext[^1] != 2) throw new Exception("Missing final record delimiter.");
        return plaintext[..^1];
    }
    static string Encode(byte[] value) => Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    public void Dispose()
    {
        stopped.Cancel();
        listener.Close();
        try { loop.GetAwaiter().GetResult(); } catch (Exception) { }
        browserKey.Dispose();
        stopped.Dispose();
    }
}