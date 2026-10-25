using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Runs only in the isolated test stack; no real device or public push service is contacted.
sealed class PushTestReceiver : IDisposable
{
    readonly HttpListener listener = new();
    readonly ConcurrentDictionary<string, int> accepted = new();
    readonly CancellationTokenSource stopped = new();
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
    public int Count(string path) => accepted.GetValueOrDefault(path);
    public async Task Wait(string path, int count)
    {
        for (var i = 0; i < 100; i++)
        {
            if (Count(path) >= count) return;
            await Task.Delay(100);
        }
        throw new Exception("Expected signed Web Push request was not accepted by the isolated receiver.");
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
                    || request.HttpMethod != "POST" || request.ContentLength64 != 0 || request.Headers["Content-Encoding"] != null
                    || request.Headers["TTL"] != "86400" || header.RootElement.GetProperty("alg").GetString() != "ES256"
                    || claims.RootElement.GetProperty("aud").GetString() != Origin
                    || claims.RootElement.GetProperty("exp").GetInt64() <= DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    || !key.VerifyData(Encoding.ASCII.GetBytes(parts[0] + "." + parts[1]), Decode(parts[2]), HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
                    throw new Exception("Invalid Web Push request.");
                var path = request.Url!.AbsolutePath;
                if (path.StartsWith("/gone/", StringComparison.Ordinal)) context.Response.StatusCode = 410;
                else
                {
                    accepted.AddOrUpdate(path, 1, (_, count) => count + 1);
                    context.Response.StatusCode = 201;
                }
            }
            catch { context.Response.StatusCode = 400; }
            finally { context.Response.Close(); }
        }
    }
    static byte[] Decode(string value) => Convert.FromBase64String(value.Replace('-', '+').Replace('_', '/') + new string('=', (4 - value.Length % 4) % 4));
    public void Dispose()
    {
        stopped.Cancel();
        listener.Close();
        try { loop.GetAwaiter().GetResult(); } catch (Exception) { }
        stopped.Dispose();
    }
}
