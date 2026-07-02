namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class UpdateTopupTransactionRequest
{
    public string CustomerID { get; init; } = string.Empty;
    public string Remarks { get; init; } = string.Empty;
}
