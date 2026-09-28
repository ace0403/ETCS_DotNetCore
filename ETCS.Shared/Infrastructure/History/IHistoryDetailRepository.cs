namespace ETCS.Shared.Infrastructure.History;

public interface IHistoryDetailRepository
{
    Task<AccessLogHeaderDto?> GetAccessLogHeaderAsync(
        int guardianId,
        long accessLogId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoryLineItemDto>> GetPosPurchaseDetailAsync(
        AccessLogHeaderDto header,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<HistoryLineItemDto>> GetLegacyMealOrderDetailAsync(
        AccessLogHeaderDto header,
        CancellationToken cancellationToken);

    Task<int?> GetAccessLogTransactionTypeByGatewayTransactionAsync(
        int guardianId,
        string gatewayTransactionId,
        CancellationToken cancellationToken);
}
