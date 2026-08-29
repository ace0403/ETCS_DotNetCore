using ETCS.Shared.Infrastructure.Meals;

namespace ETCS.Web.Infrastructure.AlaCarte;

public sealed class NutritionHighlightDto
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public string Unit { get; init; } = string.Empty;
    public string IconClass { get; init; } = string.Empty;
    public string? Emoji { get; init; }
}

public static class AlaCarteNutritionHelper
{
    public static string FormatIconClass(NutritionItemDto item)
    {
        var raw = item.ClassName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            raw = ResolveFallbackIcon(item.NutritionName);
        }

        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        return raw.Contains("fa-", StringComparison.OrdinalIgnoreCase) && !raw.Contains("fa ", StringComparison.Ordinal)
            ? $"fa {raw}"
            : raw;
    }

    public static string ResolveToneClass(string? nutritionName)
    {
        var name = nutritionName?.Trim() ?? string.Empty;
        if (name.Length == 0)
        {
            return "is-default";
        }

        if (Contains(name, "energy") || Contains(name, "calor"))
        {
            return "is-energy";
        }

        if (Contains(name, "protein"))
        {
            return "is-protein";
        }

        if (Contains(name, "carb") || Contains(name, "carbohydrate"))
        {
            return "is-carb";
        }

        if (Contains(name, "fat"))
        {
            return "is-fat";
        }

        return "is-default";
    }

    /// <summary>
    /// Available Energy, Protein, Fats, and Carbs rows for the meal card nutrition strip.
    /// Missing nutrients are omitted.
    /// </summary>
    public static IReadOnlyList<NutritionHighlightDto> GetPrimaryStats(IEnumerable<NutritionItemDto>? nutritionList)
    {
        var items = nutritionList?.ToList() ?? [];
        if (items.Count == 0)
        {
            return [];
        }

        return new NutritionHighlightDto?[]
            {
                TryBuildStat(items, "energy", "Energy", "ti ti-bolt", "kcal", null, "energy", "calor"),
                TryBuildStat(items, "protein", "Protein", "fa fa-dumbbell", "g", "💪", "protein"),
                TryBuildStat(items, "fat", "Fats", "ti ti-droplet", "g", null, "fat"),
                TryBuildStat(items, "carb", "Carbs", "ti ti-seeding", "g", null, "carb", "carbohydrate")
            }
            .Where(stat => stat is not null)
            .Select(stat => stat!)
            .ToList();
    }

    /// <summary>
    /// Returns a short calorie badge label (e.g. "450 cal") when nutrition includes energy/calories; otherwise null.
    /// </summary>
    public static string? TryFormatCalorieBadge(IEnumerable<NutritionItemDto>? nutritionList)
    {
        if (nutritionList is null)
        {
            return null;
        }

        foreach (var item in nutritionList)
        {
            var name = item.NutritionName?.Trim() ?? string.Empty;
            if (name.Length == 0)
            {
                continue;
            }

            if (!Contains(name, "energy") && !Contains(name, "calor"))
            {
                continue;
            }

            var unit = string.IsNullOrWhiteSpace(item.MeasureTypeName)
                ? "cal"
                : item.MeasureTypeName.Trim();
            if (unit.Equals("kcal", StringComparison.OrdinalIgnoreCase)
                || unit.Equals("cal", StringComparison.OrdinalIgnoreCase)
                || unit.Equals("calories", StringComparison.OrdinalIgnoreCase))
            {
                unit = "cal";
            }

            return $"{item.MeasureValue.ToString("0.#")} {unit}";
        }

        return null;
    }

    private static NutritionHighlightDto? TryBuildStat(
        IReadOnlyList<NutritionItemDto> items,
        string key,
        string label,
        string iconClass,
        string defaultUnit,
        string? emoji,
        params string[] nameNeedles)
    {
        var match = items.FirstOrDefault(item =>
            nameNeedles.Any(needle => Contains(item.NutritionName ?? string.Empty, needle)));
        if (match is null)
        {
            return null;
        }

        var unit = string.IsNullOrWhiteSpace(match.MeasureTypeName)
            ? defaultUnit
            : match.MeasureTypeName.Trim();

        return new NutritionHighlightDto
        {
            Key = key,
            Label = label,
            Value = match.MeasureValue.ToString("0.#"),
            Unit = unit,
            IconClass = iconClass,
            Emoji = emoji
        };
    }

    private static string ResolveFallbackIcon(string? nutritionName)
    {
        var name = nutritionName?.Trim() ?? string.Empty;
        if (Contains(name, "energy") || Contains(name, "calor"))
        {
            return "fa-fire";
        }

        if (Contains(name, "protein") || Contains(name, "fat") || Contains(name, "carb"))
        {
            return "fa-square";
        }

        return string.Empty;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
