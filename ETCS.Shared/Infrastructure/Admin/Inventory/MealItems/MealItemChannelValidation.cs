using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public static class MealItemChannelValidation
{
    public const string ExclusivePosMessage = "POS cannot be combined with Meal Plans or A La Carte.";

    public static bool IsPosOnly(IReadOnlyList<int>? orderTypeIds) =>
        orderTypeIds is { Count: 1 } && orderTypeIds[0] == (int)TransactionTypeEnum.POS;

    public static bool HasPos(IReadOnlyList<int>? orderTypeIds) =>
        orderTypeIds?.Contains((int)TransactionTypeEnum.POS) == true;

    public static bool HasMenuChannels(IReadOnlyList<int>? orderTypeIds) =>
        orderTypeIds?.Any(id =>
            id == (int)TransactionTypeEnum.MealOrder || id == (int)TransactionTypeEnum.A_La_Carte) == true;

    public static bool HasMealPlan(IReadOnlyList<int>? orderTypeIds) =>
        orderTypeIds?.Contains((int)TransactionTypeEnum.MealOrder) == true;

    public static List<int> NormalizeOrderTypeIds(IReadOnlyList<int> orderTypeIds)
    {
        var list = orderTypeIds.Distinct().ToList();
        if (!HasMenuChannels(list))
        {
            return list;
        }

        var mealPlanId = (int)TransactionTypeEnum.MealOrder;
        var alaCarteId = (int)TransactionTypeEnum.A_La_Carte;
        if (!list.Contains(mealPlanId))
        {
            list.Add(mealPlanId);
        }

        if (!list.Contains(alaCarteId))
        {
            list.Add(alaCarteId);
        }

        return list;
    }

    public static string? ValidateChannelCombination(IReadOnlyList<int>? orderTypeIds)
    {
        if (orderTypeIds is null or { Count: 0 })
        {
            return null;
        }

        var invalidIds = orderTypeIds
            .Distinct()
            .Where(id => !MealItemChannelOptionIds.Selectable.Contains(id))
            .ToList();
        if (invalidIds.Count > 0)
        {
            return "One or more channels are not valid.";
        }

        return HasPos(orderTypeIds) && HasMenuChannels(orderTypeIds)
            ? ExclusivePosMessage
            : null;
    }
}
