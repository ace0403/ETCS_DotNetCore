namespace ETCS.PaymentGateway.Models;

public sealed class PaymentSessionCreateResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string RedirectUrl { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;
}
