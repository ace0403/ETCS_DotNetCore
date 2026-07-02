namespace ETCS.Shared.Infrastructure.Meals;

public sealed class MealItemSlimDto
{
    public int Id { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public IReadOnlyList<NutritionItemDto> NutritionList { get; init; } = [];

    public string StudentAllergies { get; init; } = string.Empty;
}
