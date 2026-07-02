using ETCS.Shared.Infrastructure.Meals;

namespace ETCS.Web.Models;

public sealed class AlaCarteMealTypeGroup
{
    public string MealTypeId { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealCssClass { get; init; } = string.Empty;

    public IReadOnlyList<MealItemDto> MealItems { get; init; } = [];
}
