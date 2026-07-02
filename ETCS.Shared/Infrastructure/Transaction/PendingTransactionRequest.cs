namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class PendingTransactionRequest
{
    public string CustomerID { get; init; } = string.Empty;

    public string Creby { get; init; } = string.Empty;

    public string Amount { get; init; } = string.Empty;

    public string Loaded { get; init; } = string.Empty;

    public string TransDate { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public string Mode { get; init; } = "O";

    public string BankName { get; init; } = "ETISALAT";

    public string PaymentDetails { get; init; } = string.Empty;

    public string Billdate { get; init; } = string.Empty;

    public string RequestObject { get; init; } = string.Empty;
}
