using ETCS.Shared.Application.Orders.Summaries;

namespace ETCS.Web.Models;

public sealed class HistoryPageViewModel
{
    public IReadOnlyList<HistoryChildOption> Children { get; init; } = [];

    public int? SelectedStudentId { get; init; }

    public string SelectedType { get; init; } = "all";

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public IReadOnlyList<HistoryListItemViewModel> Items { get; init; } = [];

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class HistoryChildOption
{
    public int StudentId { get; init; }

    public string Name { get; init; } = string.Empty;
}

public sealed class HistoryListItemViewModel
{
    public int Id { get; init; }

    public long AccessLogId { get; init; }

    public string TransactionType { get; init; } = "topup";

    public int? OrderTypeId { get; init; }

    public string TypeLabel { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool IsCredit { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public DateTime CreatedOn { get; init; }

    public bool HasDetail { get; init; }

    public string DetailUrl { get; init; } = string.Empty;
}

public sealed class HistoryTopupDetailViewModel
{
    public int Id { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }
}

public sealed class HistoryLineItemViewModel
{
    public string ItemName { get; init; } = string.Empty;

    public string SkuCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime? DeliveryDate { get; init; }
}

public sealed class HistoryAccessLogDetailViewModel
{
    public string StatusLabel { get; init; } = "Completed";

    public string StatusCss { get; init; } = "is-success";

    public string TypeLabel { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTime CreatedOn { get; init; }

    public IReadOnlyList<HistoryLineItemViewModel> Lines { get; init; } = [];
}

public sealed class HistoryDetailViewModel
{
    public HistoryOrderDetailViewModel? Order { get; init; }

    public HistoryAccessLogDetailViewModel? AccessLog { get; init; }
}

public sealed class HistoryOrderDetailViewModel
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }

    public int OrderTypeId { get; init; }

    public AlaCarteSummaryViewModel? AlaCarteSummary { get; init; }

    public MealComboSummaryViewModel? ComboSummary { get; init; }
}
