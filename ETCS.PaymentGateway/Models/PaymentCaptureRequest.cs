namespace ETCS.PaymentGateway.Models;

public sealed record PaymentCaptureRequest(
    string TransactionId,
    string OrderId,
    int StudentId,
    bool UseNativeCredentials = false)
{
    /// <summary>Server-owned amount from the original V1 top-up record.</summary>
    public decimal? ExpectedAmount { get; init; }
}
