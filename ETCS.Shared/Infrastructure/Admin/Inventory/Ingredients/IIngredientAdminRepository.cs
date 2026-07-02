using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Ingredients;

public interface IIngredientAdminRepository
{
    Task<DataTableResponse<IngredientListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngredientListItemDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<IngredientSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveAsync(IngredientSaveRequest request, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
