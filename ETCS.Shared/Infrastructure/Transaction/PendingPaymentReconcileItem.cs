namespace ETCS.Shared.Infrastructure.Transaction;

public enum PendingPaymentReconcileKind
{
    Topup = 1,
    Order = 2
}

public sealed class PendingPaymentReconcileItem
{
    public PendingPaymentReconcileKind Kind { get; init; }

    public int TransactionPkId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public int StatusId { get; init; }

    public int ReconcileAttemptCount { get; init; }
}
