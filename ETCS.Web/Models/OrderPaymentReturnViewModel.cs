namespace ETCS.Web.Models;

public sealed class OrderPaymentReturnViewModel
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public int OrderTypeId { get; init; }

    public AlaCarteSummaryViewModel? AlaCarteSummary { get; init; }

    public MealComboSummaryViewModel? ComboSummary { get; init; }
}
