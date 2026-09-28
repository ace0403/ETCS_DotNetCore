namespace ETCS.Shared.Application.Orders.Summaries;

public class OrderPaymentReceiptDto
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public int OrderTypeId { get; init; }

    public AlaCarteSummaryViewModel? AlaCarteSummary { get; init; }

    public MealComboSummaryViewModel? ComboSummary { get; init; }
}

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

public sealed class MealComboSummaryViewModel
{
    public decimal OrderAmount { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public IReadOnlyList<MealComboSummaryItem> SelectedLines { get; init; } = [];

    public int ItemCount => SelectedLines.Count;

    public int DayCount => SelectedLines.Select(x => x.MealDate.Date).Distinct().Count();
}

public sealed class MealComboSummaryItem
{
    public int Id { get; init; }

    public Guid SelectionId { get; init; }

    public bool IsAddon { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string DisplayName => IsAddon ? ItemName : PackageName;

    public string ItemsName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealSessionName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public DateTime MealDate { get; init; }

    public string? ImageName { get; init; }
}
