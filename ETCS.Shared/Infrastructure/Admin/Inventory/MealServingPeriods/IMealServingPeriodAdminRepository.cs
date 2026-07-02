using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealServingPeriods;

public interface IMealServingPeriodAdminRepository
{
    Task<DataTableResponse<MealServingPeriodListDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default);

    Task<MealServingPeriodSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveAsync(MealServingPeriodSaveRequest request, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
