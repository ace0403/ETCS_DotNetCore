namespace ETCS.Shared.Infrastructure.Transaction;

public interface ITransactionRepository
{
    Task<bool> PendingOrderIdExistsAsync(string orderId, CancellationToken cancellationToken);

    Task LogPaymentRequestAsync(string transactionId, string result, CancellationToken cancellationToken);

    Task InsertPendingTransactionAsync(PendingTransactionRequest request, CancellationToken cancellationToken);

    Task UpdatePendingTransactionAsync(UpdatePendingTransactionRequest request, CancellationToken cancellationToken);

    Task UpdateTopupTransactionAsync(UpdateTopupTransactionRequest request, CancellationToken cancellationToken);

    Task UpdatePendingAndTopupTransactionAsync(
        UpdatePendingTransactionRequest pendingRequest,
        UpdateTopupTransactionRequest topupRequest,
        CancellationToken cancellationToken);

    Task<int> CreateTopupPendingTransactionAsync(TopupTransactionCreateRequest request, CancellationToken cancellationToken);

    Task UpdateTopupTransactionStatusAsync(TopupTransactionUpdateRequest request, CancellationToken cancellationToken);

    Task<TopupPendingTransactionState?> GetTopupPendingByOrderIdAsync(
        string orderId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken);

    Task<TopupPendingTransactionState?> GetTopupPendingForCompletionAsync(
        string orderId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingPaymentReconcileItem>> ListPendingForReconcileAsync(
        int lookbackHours,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken);

    Task BumpReconcileAttemptAsync(int transactionPkId, CancellationToken cancellationToken);

    Task QueueEmailNotificationAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken);

    Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
        int? studentId,
        int? guardianId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<TransactionHistoryItemDto?> GetGuardianTransactionByIdAsync(
        int guardianId,
        int transactionId,
        CancellationToken cancellationToken);
}
