using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Application.History;

public interface IGuardianHistoryService
{
    GuardianHistoryListItemDto MapListItem(TransactionHistoryItemDto item);

    GuardianHistoryListResponse MapListResponse(TransactionHistoryResponse response);

    Task<GuardianHistoryDetailResponse?> GetOrderDetailAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken);

    Task<GuardianHistoryDetailResponse?> GetAccessLogDetailAsync(
        int guardianId,
        long aid,
        CancellationToken cancellationToken);

    Task<GuardianHistoryDetailResponse?> GetTopupDetailAsync(
        int guardianId,
        int transactionId,
        CancellationToken cancellationToken);
}
