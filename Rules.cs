using System.Text;
using System.Security.Cryptography;

namespace ShoppingGroups.Domain;

public sealed class RuleException(int status, string code, string message) : Exception(message)
{
    public int Status { get; } = status;
    public string Code { get; } = code;
}

public static class Rules
{
    public static string Text(string? value, string field, int max = 200)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Length > max)
            throw new RuleException(400, "validation", $"{field}: въведете между 1 и {max} знака.");
        return text;
    }

    public static string Theme(string? value) => value is "light" or "dark" ? value
        : throw new RuleException(400, "theme", "Изберете светла или тъмна тема.");

    public static string Normalize(string value) => string.Join(' ', value.Normalize(NormalizationForm.FormKC)
        .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).ToUpperInvariant();

    // Bound the indexed value even when Unicode compatibility normalization greatly expands text.
    public static string DuplicateKey(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Normalize(value))));

    public static bool Completed(IEnumerable<bool> purchased)
    {
        var items = purchased.ToArray();
        return items.Length > 0 && items.All(x => x);
    }

    public static bool CanRestore(DateTime? deletedAt, DateTime now) =>
        deletedAt.HasValue && now < deletedAt.Value.AddDays(90);

    public static void CheckVersion(Guid actual, Guid expected)
    {
        if (actual != expected) throw new RuleException(409, "conflict", "Има по-нова промяна. Обновете и опитайте отново.");
    }

    public static readonly string[] ProductChanges = ["item.added", "item.edited", "item.deleted", "item.purchased", "item.unpurchased"];

    public static string Summary(IEnumerable<(string Kind, string Text)> changes)
    {
        var entries = changes.Where(x => ProductChanges.Contains(x.Kind)).ToList();
        if (entries.Count == 0) throw new RuleException(400, "no_changes", "Няма нови промени за изпращане.");
        var labels = new Dictionary<string, string> { ["item.added"] = "Добавени", ["item.edited"] = "Променени",
            ["item.deleted"] = "Изтрити", ["item.purchased"] = "Купени", ["item.unpurchased"] = "Върнати за покупка" };
        return string.Join(" · ", entries.GroupBy(x => x.Kind).Select(g => $"{labels[g.Key]}: {g.Count()}"))
            + "\n" + string.Join("\n", entries.TakeLast(20).Select(x => x.Text));
    }
}
