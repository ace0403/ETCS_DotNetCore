using ETCS.Shared.Application.Orders;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Payment;

/// <summary>Native-only validation; web/V1 order behavior is unchanged.</summary>
public sealed class NativeOrderValidator(IMealRepository menus, OrderAllergenConsentEnricher consent)
{
    public async Task<(IReadOnlyList<OrderMealLineItemRequest> Lines, string? Error)> ValidateAsync(
        NativeOrderInitiateRequest request, int schoolId, CancellationToken cancellationToken)
    {
        if (request.MealList.Count == 0 || request.Total <= 0 || schoolId <= 0)
            return (request.MealList, "A student school, meals and a positive total are required.");
        decimal total = 0;
        foreach (var day in request.MealList.GroupBy(x => x.MealDate.Date))
        {
            var items = day.Any(x => x.ItemId is > 0)
                ? await menus.GetMealItemsForStudentAsync(request.StudentId, schoolId, day.Key, cancellationToken: cancellationToken)
                : [];
            var packages = day.Any(x => x.PackageId is > 0)
                ? await menus.GetMealPackagesForStudentAsync(request.StudentId, schoolId, day.Key, cancellationToken: cancellationToken)
                : [];
            foreach (var line in day)
            {
                if (line.Quantity <= 0 || (line.ItemId is > 0) == (line.PackageId is > 0))
                    return (request.MealList, "Each meal needs a positive quantity and exactly one item or package.");
                decimal? unitPrice = line.ItemId is > 0
                    ? items.FirstOrDefault(x => x.Id == line.ItemId)?.Price
                    : packages.FirstOrDefault(x => x.Id == line.PackageId)?.TotalPrice;
                if (unitPrice is null || unitPrice <= 0)
                    return (request.MealList, "A selected meal is unavailable. Please refresh the menu.");
                var subtotal = decimal.Round(unitPrice.Value * line.Quantity, 2, MidpointRounding.AwayFromZero);
                // Mobile sends Price and Total as the line subtotal, matching the existing order contract.
                if (line.Price != subtotal || line.Total != subtotal)
                    return (request.MealList, "Meal prices have changed. Please refresh your cart.");
                total += subtotal;
            }
        }
        if (request.Total != total)
            return (request.MealList, "Total does not match the current menu prices.");
        var result = await consent.EnrichMealListAsync(request.StudentId, request.MealList, cancellationToken);
        return (result.MealList, result.ErrorMessage);
    }
}
