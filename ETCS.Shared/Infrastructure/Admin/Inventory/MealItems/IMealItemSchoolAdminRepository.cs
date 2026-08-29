using System.Data.Common;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public interface IMealItemSchoolAdminRepository
{
    Task<IReadOnlyList<int>> GetSchoolIdsAsync(int mealItemId, CancellationToken cancellationToken = default);

    Task SaveSchoolIdsAsync(
        int mealItemId,
        IReadOnlyList<int> schoolIds,
        CancellationToken cancellationToken = default);

    Task SaveSchoolIdsAsync(
        int mealItemId,
        IReadOnlyList<int> schoolIds,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task DeleteSchoolIdsAsync(int mealItemId, CancellationToken cancellationToken = default);
}
