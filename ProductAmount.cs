using System.Globalization;

namespace ShoppingGroups.Domain;

public readonly record struct ProductAmount(decimal Quantity, string Unit)
{
    public static readonly IReadOnlyDictionary<string, string> Units = new Dictionary<string, string> {
        ["pcs"] = "бр.", ["kg"] = "кг", ["g"] = "г", ["l"] = "л", ["ml"] = "мл", ["pack"] = "пак."
    };

    public static ProductAmount Create(decimal quantity = 1, string unit = "pcs")
    {
        if (quantity <= 0 || quantity > 999999.999m || decimal.Round(quantity, 3) != quantity)
            throw new RuleException(400, "quantity", "Количеството трябва да е над 0, до 999999,999 и с най-много 3 цифри след запетаята.");
        if (unit == null || !Units.ContainsKey(unit))
            throw new RuleException(400, "unit", "Изберете мерна единица от списъка.");
        return new(quantity, unit);
    }

    public string Label => $"{Quantity.ToString("0.###", CultureInfo.GetCultureInfo("bg-BG"))} {Units[Unit]}";
    public string Describe(string text) => $"{text} · {Label}";
}
