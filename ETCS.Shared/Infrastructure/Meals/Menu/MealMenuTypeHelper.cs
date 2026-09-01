using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public static class MealMenuTypeHelper
{
    public const string AllFilterKey = "all";

    public static string ResolveFilterKey(string? mealTypeId, string? mealTypeName)
    {
        if (!string.IsNullOrWhiteSpace(mealTypeId))
        {
            return mealTypeId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(mealTypeName))
        {
            return "name:" + mealTypeName.Trim().ToLowerInvariant();
        }

        return "0";
    }

    public static string ResolveDisplayName(string? mealTypeName) =>
        string.IsNullOrWhiteSpace(mealTypeName) ? "Meals" : mealTypeName.Trim();

    public static string ResolveSubtitle(string? sessionName)
    {
        var name = sessionName?.Trim() ?? string.Empty;
        if (name.Contains("breakfast", StringComparison.OrdinalIgnoreCase))
        {
            return "Snacks, Sandwiches, Dairy & Drinks";
        }

        if (name.Contains("lunch", StringComparison.OrdinalIgnoreCase))
        {
            return "Meal Packages, Dairy & Drinks";
        }

        if (name.Contains("dinner", StringComparison.OrdinalIgnoreCase)
            || name.Contains("supper", StringComparison.OrdinalIgnoreCase))
        {
            return "Meal Packages & Drinks";
        }

        return "Meal packages and add-ons";
    }

    public static IReadOnlyList<MealMenuTypeFilterDto> BuildSortedMealTypeFilters(
        IEnumerable<(string MealTypeId, string MealTypeName)> sources,
        IReadOnlyList<MealEnumLookupDto> sessionMealTypes)
    {
        var filters = new List<MealMenuTypeFilterDto>
        {
            new()
            {
                FilterKey = AllFilterKey,
                TypeName = "All",
                SortOrder = int.MinValue
            }
        };

        var groupedSources = sources
            .GroupBy(s => ResolveFilterKey(s.MealTypeId, s.MealTypeName))
            .ToDictionary(
                g => g.Key,
                g => ResolveDisplayName(g.First().MealTypeName),
                StringComparer.Ordinal);

        if (groupedSources.Count == 0)
        {
            return filters;
        }

        var remainingKeys = new HashSet<string>(groupedSources.Keys, StringComparer.Ordinal);

        foreach (var mealType in sessionMealTypes.OrderBy(t => t.SortOrder).ThenBy(t => t.Name, StringComparer.OrdinalIgnoreCase))
        {
            var filterKey = mealType.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!remainingKeys.Remove(filterKey))
            {
                continue;
            }

            var typeName = string.IsNullOrWhiteSpace(mealType.Name)
                ? groupedSources[filterKey]
                : mealType.Name.Trim();
            if (!ShouldShowTypeChip(filterKey, typeName))
            {
                continue;
            }

            filters.Add(new MealMenuTypeFilterDto
            {
                FilterKey = filterKey,
                TypeName = typeName,
                SortOrder = mealType.SortOrder
            });
        }

        foreach (var filterKey in remainingKeys.OrderBy(key => key, StringComparer.Ordinal))
        {
            var typeName = groupedSources[filterKey];
            if (!ShouldShowTypeChip(filterKey, typeName))
            {
                continue;
            }

            filters.Add(new MealMenuTypeFilterDto
            {
                FilterKey = filterKey,
                TypeName = typeName,
                SortOrder = int.MaxValue
            });
        }

        return filters;
    }

    private static bool ShouldShowTypeChip(string filterKey, string displayName)
    {
        if (filterKey == "0")
        {
            return false;
        }

        return !string.Equals(displayName, "Meals", StringComparison.OrdinalIgnoreCase)
            || filterKey.StartsWith("name:", StringComparison.Ordinal);
    }
}
