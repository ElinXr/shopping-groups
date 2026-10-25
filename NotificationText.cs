using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace ShoppingGroups.Domain;

public sealed record NotificationContent(string Title, string Text);

public static class NotificationText
{
    static readonly TimeZoneInfo Sofia = TimeZoneInfo.FindSystemTimeZoneById("Europe/Sofia");
    static readonly JsonSerializerOptions JsonOptions = new() { Encoder = JavaScriptEncoder.Create(UnicodeRanges.All) };
    static readonly IReadOnlyDictionary<string, (string Prefix, string Verb)> Actions = new Dictionary<string, (string, string)> {
        ["item.added"] = ("Добавено: ", "добави"), ["item.edited"] = ("Променено: ", "промени"),
        ["item.deleted"] = ("Изтрито: ", "премахна"), ["item.purchased"] = ("Купено: ", "купи"),
        ["item.unpurchased"] = ("Върнато за покупка: ", "върна за покупка")
    };

    public static string Time(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Sofia)
        .ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Summary(IEnumerable<(string Kind, string Text, string ActorName, DateTime At)> changes)
    {
        var entries = changes.Where(x => Actions.ContainsKey(x.Kind)).ToArray();
        if (entries.Length == 0) throw new RuleException(400, "no_changes", "Няма нови промени за изпращане.");
        var lines = entries.TakeLast(20).Reverse().Select(x => {
            var action = Actions[x.Kind];
            var product = x.Text.StartsWith(action.Prefix, StringComparison.Ordinal) ? x.Text[action.Prefix.Length..] : x.Text;
            var arrow = product.IndexOf(" → ", StringComparison.Ordinal);
            if (x.Kind == "item.edited" && arrow > 0)
                return $"{x.ActorName} промени „{product[..arrow]}“ на „{product[(arrow + 3)..]}“ ({Time(x.At)}).";
            return $"{x.ActorName} {action.Verb} „{product}“ ({Time(x.At)}).";
        }).ToList();
        if (entries.Length > 20) lines.Add($"И още {entries.Length - 20} промени. Вижте пълната история в списъка.");
        return string.Join('\n', lines);
    }

    public static NotificationContent Compose(string kind, string listName, string actor, string summary) => kind switch {
        "summary" => new($"Промени в „{listName}“", summary),
        "started" => new($"{actor} започна да пазарува", $"Можете да се включите в пазаруването по „{listName}“."),
        "completed" => new($"Списъкът „{listName}“ е готов", "Всичко в списъка е отбелязано като купено."),
        _ => new("Ново от вашата група", summary)
    };

    public static byte[] Payload(string text, DateTime at, string? title = null)
    {
        // Older saved notices retain their author-first preview when queued across an update.
        var lines = text.Split('\n').ToList();
        var lastDetail = lines.FindLastIndex(line => line.Contains(" — ", StringComparison.Ordinal));
        if (lastDetail > 0) { var detail = lines[lastDetail]; lines.RemoveAt(lastDetail); lines.Insert(0, detail); }
        var body = string.Join('\n', lines);
        title = string.IsNullOrWhiteSpace(title) ? "Shopping Groups" : title;
        if (title.Length > 120) title = title[..(char.IsHighSurrogate(title[119]) ? 119 : 120)];
        while (true)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(new { version = 1, title, body, at }, JsonOptions);
            if (json.Length <= 3000) return json;
            var keep = Math.Max(0, body.Length * 2700 / json.Length - 8);
            if (keep > 0 && char.IsHighSurrogate(body[keep - 1])) keep--;
            body = body[..keep] + "\n…";
        }
    }
}
