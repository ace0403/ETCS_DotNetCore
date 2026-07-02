namespace ETCS.PaymentGateway.Models;

public sealed class PaymentCaptureResult
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string Message { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;
}
