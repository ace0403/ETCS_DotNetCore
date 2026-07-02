namespace ETCS.Shared.Infrastructure.Meals;

public sealed class NutritionItemDto
{
    public int Id { get; init; }

    public int NutritionId { get; init; }

    public string NutritionName { get; init; } = string.Empty;

    public string MeasureTypeName { get; init; } = string.Empty;

    public decimal MeasureValue { get; init; }

    public string ClassName { get; init; } = string.Empty;
}
