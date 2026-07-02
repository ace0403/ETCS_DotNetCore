namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TopupTransactionUpdateRequest
{
    public int TransactionPkId { get; init; }

    public string GatewayTransactionId { get; init; } = string.Empty;

    public int StatusId { get; init; }

    public bool IsTransactionCompleted { get; init; }

    public string Remarks { get; init; } = string.Empty;

    public int? UpdatedBy { get; init; }
}
