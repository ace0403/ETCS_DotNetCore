using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public interface IMealItemAdminRepository
{
    Task<DataTableResponse<MealItemListDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<MealItemSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MealItemListDto>> ListBySchoolAsync(int schoolId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(MealItemSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
