using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosOrderInitiateRequest
{
    public int StudentId { get; init; }
    public string TerminalCode { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public decimal Total { get; init; }
    public string Notes { get; init; } = string.Empty;
    public IReadOnlyList<OrderMealLineItemRequest> MealList { get; init; } = [];
}

public sealed class PosOrderCompleteRequest
{
    public string OrderId { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string IbonusTransactionId { get; init; } = string.Empty;
    public string TerminalCode { get; init; } = string.Empty;
}

public sealed class PosOrderUndoRequest
{
    public string OrderId { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string CustomerId { get; init; } = string.Empty;
    public string IbonusTransactionId { get; init; } = string.Empty;
    public string TerminalCode { get; init; } = string.Empty;
}

public sealed class PosOrderInitiateResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public int GuardianId { get; init; }
    public decimal Total { get; init; }
    public int MealTransactionId { get; init; }
}

public sealed class PosOrderCompleteResponse
{
    public bool IsSuccess { get; init; }
    public bool IsAlreadyProcessed { get; init; }
    public string Message { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string IbonusTransactionId { get; init; } = string.Empty;
    public long AccessLogId { get; init; }
}
