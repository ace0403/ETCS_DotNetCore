namespace ETCS.Shared.Infrastructure.Payment;

public sealed class PaymentStatusResponse
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public bool IsAlreadyProcessed { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}
