namespace ETCS.Shared.Infrastructure.Orders;

public interface IMealOrderRepository
{
    Task<bool> OrderIdExistsAsync(
        string orderId,
        CancellationToken cancellationToken);

    Task<OrderSpendingSnapshot> GetSpendingSnapshotAsync(
        int studentId,
        int guardianId,
        DateTime referenceDate,
        CancellationToken cancellationToken);

    Task<int> CreatePendingOrderAsync(
        OrderInitiateRequest request,
        int transactionStatusId,
        CancellationToken cancellationToken);

    Task SetPaymentSessionAsync(
        string orderId,
        string gatewayTransactionId,
        int transactionStatusId,
        CancellationToken cancellationToken);

    Task SetPaymentSessionFailedAsync(
        string orderId,
        string message,
        CancellationToken cancellationToken);

    Task<MealOrderPaymentState?> GetPaymentStateAsync(
        string orderId,
        CancellationToken cancellationToken);

    Task<string?> GetGatewayTransactionIdByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken);

    Task<MealOrderPaymentState?> GetPaymentStateForCompletionAsync(
        string orderId,
        CancellationToken cancellationToken);

    Task MarkPaymentCompletedAsync(
        string orderId,
        string gatewayTransactionId,
        int paymentStatusId,
        int paidOrderStatusId,
        CancellationToken cancellationToken);

    Task AttachAccessLogIdAsync(
        string orderId,
        long accessLogId,
        CancellationToken cancellationToken);

    Task<OrderListResponse> GetOrderListAsync(
        int guardianId,
        int? studentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken);

    Task<OrderDetailDto?> GetOrderDetailByOrderIdAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Meal activity for a guardian (optional student) in a date range from AccessLog:
    /// Meal Plan count (Description = Meal Plan), Parent Orders total (Description = Meal Order),
    /// Canteen Purchases total (TransactionType 1004/2004).
    /// </summary>
    Task<MealActivitySummaryRow> GetMealActivitySummaryAsync(
        int guardianId,
        int? studentId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<OrderCalendarItemDto>> GetOrderCalendarItemsAsync(
        int guardianId,
        int? studentId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken);
}
