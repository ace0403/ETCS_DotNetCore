namespace ETCS.Shared.Infrastructure.Orders;

public sealed class MealActivitySummaryResponse
{
    public int Year { get; init; }

    public int Month { get; init; }

    public int MealPlanMealsUsed { get; init; }

    public decimal AlaCarteAmount { get; init; }

    public decimal PosAmount { get; init; }
}

public sealed class MealActivitySummaryRow
{
    public int MealPlanMealsUsed { get; init; }

    public decimal AlaCarteAmount { get; init; }

    public decimal PosAmount { get; init; }
}
