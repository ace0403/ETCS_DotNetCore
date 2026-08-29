using System.Data.Common;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public interface IMealItemOrderTypeAdminRepository
{
    Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(int mealItemId, CancellationToken cancellationToken = default);

    Task SaveOrderTypesAsync(
        int mealItemId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default);

    Task SaveOrderTypesAsync(
        int mealItemId,
        IReadOnlyList<int> orderTypeIds,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);

    Task DeleteOrderTypesAsync(int mealItemId, CancellationToken cancellationToken = default);
}
