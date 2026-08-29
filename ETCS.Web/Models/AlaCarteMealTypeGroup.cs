using ETCS.Shared.Infrastructure.Meals;

namespace ETCS.Web.Models;

public sealed class AlaCarteMealTypeGroup
{
    public string MealSessionId { get; init; } = string.Empty;

    public string MealSessionName { get; init; } = string.Empty;

    public string MealSessionCssClass { get; init; } = string.Empty;

    public IReadOnlyList<MealItemDto> MealItems { get; init; } = [];
}
