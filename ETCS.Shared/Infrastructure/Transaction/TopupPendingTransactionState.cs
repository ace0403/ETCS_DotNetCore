namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TopupPendingTransactionState
{
    public int TransactionPkId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public bool IsTransactionCompleted { get; init; }

    public int StatusId { get; init; }

    public decimal Amount { get; init; }

    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public string GatewayTransactionId { get; init; } = string.Empty;
}
