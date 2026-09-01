namespace ETCS.Shared.Infrastructure.Meals.Menu;

public sealed class MealMenuResponse
{
    public int StudentId { get; init; }

    public DateOnly MealDate { get; init; }

    public bool IsOrderable { get; init; }

    public string DayStatus { get; init; } = "FullDay";

    public MealClosedDayDto? ClosedDay { get; init; }

    public IReadOnlyList<MealMenuSessionDto> Sessions { get; init; } = [];
}

public sealed class MealClosedDayDto
{
    public string ClosedType { get; init; } = string.Empty;

    public string DayName { get; init; } = string.Empty;

    public string BadgeText { get; init; } = string.Empty;

    public string MessageLine1 { get; init; } = string.Empty;

    public string MessageLine2 { get; init; } = string.Empty;

    public string? Title { get; init; }
}

public sealed class MealMenuSessionDto
{
    public int MealSessionId { get; init; }

    public string MealSessionName { get; init; } = string.Empty;

    public string Subtitle { get; init; } = string.Empty;

    public string CssClass { get; init; } = string.Empty;

    public IReadOnlyList<MealMenuTypeFilterDto> MealTypeFilters { get; init; } = [];

    public IReadOnlyList<MealMenuItemDto> Items { get; init; } = [];
}

public sealed class MealMenuTypeFilterDto
{
    public string FilterKey { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

public sealed class MealMenuItemDto
{
    public int Id { get; init; }

    public bool IsAddon { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string ItemsName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public string MealTypeId { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealCategoryName { get; init; } = string.Empty;

    public IReadOnlyList<string> IngredientNames { get; init; } = [];

    public IReadOnlyList<MealIngredientDto> Ingredients { get; init; } = [];

    public string? ImageName { get; init; }

    public string? ImageUrl { get; init; }

    public string? ThumbnailUrl { get; init; }

    public IReadOnlyList<NutritionItemDto> NutritionList { get; init; } = [];

    public string StudentAllergies { get; init; } = string.Empty;

    public bool IsPopular { get; init; }
}

public sealed class MealSchoolDayDto
{
    public DateOnly Date { get; init; }

    public string Status { get; init; } = "FullDay";

    public bool IsWeekend { get; init; }

    public bool IsOrderable { get; init; }

    public string? Badge { get; init; }

    public string? ClosedType { get; init; }

    public string? Title { get; init; }
}
