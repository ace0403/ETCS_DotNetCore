using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Web.Models;

namespace ETCS.Web.Infrastructure.AlaCarte;

public static class AlaCarteMealTypeHelper
{
    public static string ResolveIcon(string? mealTypeName)
    {
        var name = mealTypeName?.Trim() ?? string.Empty;
        if (Contains(name, "breakfast")) return "ti ti-sun";
        if (Contains(name, "lunch")) return "ti ti-tools-kitchen-2";
        if (Contains(name, "dinner") || Contains(name, "supper")) return "ti ti-moon";
        if (Contains(name, "snack")) return "ti ti-cookie";
        return "ti ti-tools-kitchen-2";
    }

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

    public static string ResolveMenuTypeBadgeName(string? mealCategoryName) =>
        string.IsNullOrWhiteSpace(mealCategoryName) ? "Meals" : mealCategoryName.Trim();

    public static bool ShouldShowTypeChip(string filterKey, string displayName)
    {
        if (filterKey == "0")
        {
            return false;
        }

        return !string.Equals(displayName, "Meals", StringComparison.OrdinalIgnoreCase)
            || filterKey.StartsWith("name:", StringComparison.Ordinal);
    }

    public static IReadOnlyList<MealComboMenuCard> BuildMergedMenuCards(
        IReadOnlyList<MealPackageDto> packages,
        IReadOnlyList<MealItemDto> addonItems)
    {
        var cards = new List<MealComboMenuCard>(packages.Count + addonItems.Count);

        foreach (var package in packages)
        {
            cards.Add(new MealComboMenuCard { IsAddon = false, Package = package });
        }

        foreach (var addon in addonItems)
        {
            cards.Add(new MealComboMenuCard { IsAddon = true, Addon = addon });
        }

        return cards
            .OrderBy(card => card.IsAddon ? card.Addon!.MealTypeSortOrder : card.Package!.MealTypeSortOrder)
            .ThenBy(card => card.IsAddon ? card.Addon!.MealTypeName : card.Package!.MealTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(card => card.IsAddon)
            .ThenBy(card => card.IsAddon ? card.Addon!.ItemName : card.Package!.PackageName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<MealComboMealTypeFilterOption> BuildSortedMealTypeFilters(
        IEnumerable<(string MealTypeId, string MealTypeName)> sources,
        IReadOnlyList<MealEnumLookupDto> sessionMealTypes)
    {
        var groupedSources = sources
            .GroupBy(s => ResolveFilterKey(s.MealTypeId, s.MealTypeName))
            .ToDictionary(
                g => g.Key,
                g => ResolveDisplayName(g.First().MealTypeName),
                StringComparer.Ordinal);

        if (groupedSources.Count == 0)
        {
            return [];
        }

        var filters = new List<MealComboMealTypeFilterOption>();
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

            filters.Add(new MealComboMealTypeFilterOption
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

            filters.Add(new MealComboMealTypeFilterOption
            {
                FilterKey = filterKey,
                TypeName = typeName,
                SortOrder = int.MaxValue
            });
        }

        return filters;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
