using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ShoppingGroups.Domain;

public static class NotificationText
{
    static readonly TimeZoneInfo Sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
    static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
    public static string Time(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Sofia)
        .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Summary(IEnumerable<(string Kind, string Text, string ActorName, DateTime At)> changes) =>
        Rules.Summary(changes.Select(x => (x.Kind, $"{x.ActorName} · {Time(x.At)} — {x.Text}")));

    public static string FromActor(string actor, string text, DateTime at) => $"От {actor} · {Time(at)}\n{text}";

    public static byte[] Payload(string text, DateTime at)
    {
        var lines = text.Split('\n').ToList();
        var lastDetail = lines.FindLastIndex(line => line.Contains(" — ", StringComparison.Ordinal));
        if (lastDetail > 0)
        {
            var detail = lines[lastDetail];
            lines.RemoveAt(lastDetail);
            lines.Insert(0, detail);
        }
        var body = string.Join('\n', lines);
        while (true)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, title = "Shopping Groups", body, at }, JsonOptions);
            if (json.Length <= 3000) return json;
            var keep = Math.Max(0, body.Length * 2700 / json.Length - 8);
            if (keep > 0 && char.IsHighSurrogate(body[keep - 1])) keep--;
            body = body[..keep] + "\n…";
        }
    }
}