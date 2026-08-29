namespace ETCS.Shared.Infrastructure.Meals;

public sealed class MealPackageDto
{
    public int Id { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public string MealSessionId { get; init; } = string.Empty;

    public string MealSessionName { get; init; } = string.Empty;

    public string MealSessionCssClass { get; init; } = string.Empty;

    public string MealTypeId { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealCssClass { get; init; } = string.Empty;

    public int MealTypeSortOrder { get; init; } = int.MaxValue;

    public int? MealCategoryId { get; init; }

    public string MealCategoryName { get; init; } = string.Empty;

    public int SchoolId { get; init; }

    public string? ImageName { get; init; }

    public string? ImageUrl { get; init; }

    public string? ThumbnailUrl { get; init; }

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public decimal ProcessingFee { get; init; }

    public decimal TotalPrice => Price + ProcessingFee;

    public DateTime CreatedOn { get; init; }

    public string ItemsName { get; init; } = string.Empty;

    public int[]? WeekNo { get; init; }

    public int[]? IngredientIds { get; init; }

    public IReadOnlyList<MealIngredientDto> Ingredients { get; init; } = [];

    public IReadOnlyList<string> IngredientNames { get; init; } = [];

    public IReadOnlyList<NutritionItemDto> NutritionList { get; init; } = [];

    public string StudentAllergies { get; init; } = string.Empty;

    /// <summary>
    /// True when this package ranks among the school's top paid sellers (recent window).
    /// </summary>
    public bool IsPopular { get; init; }
}
