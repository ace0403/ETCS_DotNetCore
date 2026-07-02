using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealCombos;

public interface IMealComboAdminRepository
{
    Task<DataTableResponse<MealComboListDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<MealComboSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(MealComboSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
