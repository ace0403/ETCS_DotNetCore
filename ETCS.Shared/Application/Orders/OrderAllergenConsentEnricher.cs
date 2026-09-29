using ETCS.Shared.Infrastructure.Meals.Menu;
using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Orders;

public sealed class OrderAllergenConsentEnricher
{
    private readonly IOrderItemAllergenResolver _allergenResolver;

    public OrderAllergenConsentEnricher(IOrderItemAllergenResolver allergenResolver)
    {
        _allergenResolver = allergenResolver;
    }

    public async Task<(IReadOnlyList<OrderMealLineItemRequest> MealList, string? ErrorMessage)> EnrichMealListAsync(
        int studentId,
        IReadOnlyList<OrderMealLineItemRequest> mealList,
        CancellationToken cancellationToken)
    {
        if (mealList.Count == 0)
        {
            return (mealList, null);
        }

        var lineKeys = mealList
            .Select(ToLineKey)
            .Where(k => k.ItemId is > 0 || k.PackageId is > 0)
            .Distinct()
            .ToList();

        if (lineKeys.Count == 0)
        {
            return (mealList, null);
        }

        var allergenMap = await _allergenResolver.ResolveAsync(studentId, lineKeys, cancellationToken);
        var enriched = new List<OrderMealLineItemRequest>(mealList.Count);

        foreach (var line in mealList)
        {
            var key = ToLineKey(line);
            allergenMap.TryGetValue(key, out var allergens);
            var hasOverlap = allergens is { Count: > 0 };

            if (line.HasAllergenConsent)
            {
                if (!hasOverlap)
                {
                    return (mealList, "Allergen consent is invalid for one or more selected meals.");
                }

                enriched.Add(CloneLine(line, AllergenConsentText.FormatAllergenItemText(allergens!)));
                continue;
            }

            if (hasOverlap)
            {
                return (mealList, "Allergen consent is required for one or more selected meals.");
            }

            enriched.Add(CloneLine(line, null));
        }

        return (enriched, null);
    }

    public async Task<IReadOnlyDictionary<(int? ItemId, int? PackageId), string>> ResolveItemTextMapAsync(
        int studentId,
        IEnumerable<(int? ItemId, int? PackageId)> lines,
        CancellationToken cancellationToken)
    {
        var distinctLines = lines
            .Select(l => (
                ItemId: l.ItemId is > 0 ? l.ItemId : null,
                PackageId: l.ItemId is > 0 ? null : (l.PackageId is > 0 ? l.PackageId : null)))
            .Where(l => l.ItemId is > 0 || l.PackageId is > 0)
            .Distinct()
            .ToList();

        if (distinctLines.Count == 0)
        {
            return new Dictionary<(int? ItemId, int? PackageId), string>();
        }

        var allergenMap = await _allergenResolver.ResolveAsync(studentId, distinctLines, cancellationToken);
        return allergenMap.ToDictionary(
            kvp => kvp.Key,
            kvp => AllergenConsentText.FormatAllergenItemText(kvp.Value));
    }

    private static (int? ItemId, int? PackageId) ToLineKey(OrderMealLineItemRequest line) =>
        (
            ItemId: line.ItemId is > 0 ? line.ItemId : null,
            PackageId: line.ItemId is > 0 ? null : (line.PackageId is > 0 ? line.PackageId : null));

    private static OrderMealLineItemRequest CloneLine(OrderMealLineItemRequest line, string? allergenItemText) =>
        new()
        {
            ItemId = line.ItemId,
            PackageId = line.PackageId,
            MealDate = line.MealDate,
            Id = line.Id,
            Price = line.Price,
            Total = line.Total,
            Quantity = line.Quantity,
            HasAllergenConsent = line.HasAllergenConsent,
            AllergenItemText = allergenItemText
        };
}
