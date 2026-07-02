namespace ETCS.Web.Models;

public sealed class AlaCarteSummaryViewModel
{
    public decimal OrderAmount { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public IReadOnlyList<AlaCarteSummaryItem> SelectedMeals { get; init; } = [];

    public int ItemCount => SelectedMeals.Count;

    public int DayCount => SelectedMeals.Select(x => x.MealDate.Date).Distinct().Count();
}

public sealed class AlaCarteSummaryItem
{
    public int Id { get; init; }

    public Guid SelectionId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public decimal Price { get; init; }

    public DateTime MealDate { get; init; }

    public string? ImageName { get; init; }
}
