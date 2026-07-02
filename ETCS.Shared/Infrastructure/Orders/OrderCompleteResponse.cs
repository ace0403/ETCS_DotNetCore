namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderCompleteResponse
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public bool IsAlreadyProcessed { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public long AccessLogId { get; init; }
}
