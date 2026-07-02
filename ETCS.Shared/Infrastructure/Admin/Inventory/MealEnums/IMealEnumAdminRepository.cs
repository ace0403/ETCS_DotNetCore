namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

public interface IMealEnumAdminRepository
{
    Task<IReadOnlyList<MealEnumLookupDto>> GetByTypeIdAsync(int enumTypeId, CancellationToken cancellationToken = default);
}
