using ETCS.Shared.Application.Orders.Summaries;

namespace ETCS.Shared.Application.History;

public static class GuardianHistoryDetailKinds
{
    public const string None = "none";
    public const string Topup = "topup";
    public const string Order = "order";
    public const string AccessLog = "accessLog";
}

public sealed class GuardianHistoryListItemDto
{
    public int Id { get; init; }

    public long AccessLogId { get; init; }

    public long? Aid { get; init; }

    public bool HasMealTransaction { get; init; }

    public int GuardianId { get; init; }

    public int? StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string TransactionType { get; init; } = "topup";

    public int? OrderTypeId { get; init; }

    public int? AccessLogTransactionType { get; init; }

    public string TypeLabel { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool IsCredit { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public bool IsTransactionCompleted { get; init; }

    public int? StatusId { get; init; }

    public string Remarks { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }

    public DateTime? UpdatedOn { get; init; }

    public string PaymentMethod { get; init; } = "Unknown";

    public bool HasDetail { get; init; }

    public string DetailKind { get; init; } = GuardianHistoryDetailKinds.None;

    public int? TopupTransactionId { get; init; }
}

public sealed class GuardianHistoryListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<GuardianHistoryListItemDto> Items { get; init; } = [];
}

public sealed class GuardianHistoryLineItemDto
{
    public string ItemName { get; init; } = string.Empty;

    public string SkuCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime? DeliveryDate { get; init; }
}

public sealed class GuardianHistoryAccessLogDetailDto
{
    public string TypeLabel { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public decimal TotalAmount { get; init; }

    public DateTime CreatedOn { get; init; }

    public IReadOnlyList<GuardianHistoryLineItemDto> Lines { get; init; } = [];
}

public sealed class GuardianHistoryOrderDetailDto
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }

    public int OrderTypeId { get; init; }

    public AlaCarteSummaryViewModel? AlaCarteSummary { get; init; }

    public MealComboSummaryViewModel? ComboSummary { get; init; }
}

public sealed class GuardianHistoryTopupDetailDto
{
    public int Id { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }
}

public sealed class GuardianHistoryDetailResponse
{
    public string DetailKind { get; init; } = GuardianHistoryDetailKinds.None;

    public GuardianHistoryOrderDetailDto? Order { get; init; }

    public GuardianHistoryAccessLogDetailDto? AccessLog { get; init; }

    public GuardianHistoryTopupDetailDto? Topup { get; init; }
}
