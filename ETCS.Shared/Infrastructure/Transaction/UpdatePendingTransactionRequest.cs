namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class UpdatePendingTransactionRequest
{
    public string CustomerID { get; init; } = string.Empty;

    public string Loaded { get; init; } = string.Empty;

    public string Creby { get; init; } = string.Empty;

    public string PaymentDetails { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;
}
