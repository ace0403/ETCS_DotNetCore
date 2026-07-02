namespace ETCS.Shared.Infrastructure.Meals;

public sealed class MealPackageSlimDto
{
    public int Id { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public string ItemsName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public decimal ProcessingFee { get; init; }

    public IReadOnlyList<NutritionItemDto> NutritionList { get; init; } = [];

    public string StudentAllergies { get; init; } = string.Empty;
}
