using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Categories;

public interface ICategoryAdminRepository
{
    Task<DataTableResponse<CategoryListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CategoryListItemDto>> ListAsync(CancellationToken cancellationToken = default);
    Task<CategorySaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(CategorySaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
