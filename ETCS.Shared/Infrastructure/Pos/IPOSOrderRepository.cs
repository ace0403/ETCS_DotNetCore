using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Infrastructure.Pos;

public interface IPOSOrderRepository
{
    Task<bool> OrderIdExistsAsync(string orderId, CancellationToken cancellationToken);

    Task<OrderSpendingSnapshot> GetSpendingSnapshotAsync(
        int studentId,
        int guardianId,
        int orderTypeId,
        DateTime referenceDate,
        CancellationToken cancellationToken);

    Task<int> CreatePendingOrderAsync(
        OrderInitiateRequest request,
        int transactionStatusId,
        CancellationToken cancellationToken);

    Task<MealOrderPaymentState?> GetPaymentStateAsync(string orderId, CancellationToken cancellationToken);

    Task<MealOrderPaymentState?> GetPaymentStateForCompletionAsync(string orderId, CancellationToken cancellationToken);

    Task MarkPaymentCompletedAsync(
        string orderId,
        string ibonusTransactionId,
        int paymentStatusId,
        int paidOrderStatusId,
        CancellationToken cancellationToken);

    Task AttachAccessLogIdAsync(string orderId, long accessLogId, CancellationToken cancellationToken);
}
