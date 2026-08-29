using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealTypes;

public interface IMealTypeAdminRepository
{
    Task<DataTableResponse<MealSessionListItemDto>> GetSessionDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default);

    Task<DataTableResponse<MealTypeListItemDto>> GetTypeDataAsync(
        DataTableRequest request,
        int? sessionId,
        CancellationToken cancellationToken = default);

    Task<MealTypeSaveRequest?> GetAsync(
        int id,
        string kind,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveAsync(
        MealTypeSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult> DeleteAsync(
        int id,
        string kind,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealSessionListItemDto>> ListSessionsAsync(
        bool activeOnly = false,
        int? includeSessionId = null,
        CancellationToken cancellationToken = default);
}
