namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

public interface IMealEnumAdminRepository
{
    Task<IReadOnlyList<MealEnumLookupDto>> GetByTypeIdAsync(int enumTypeId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealEnumLookupDto>> GetMealSessionsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealEnumLookupDto>> GetMealTypesBySessionAsync(int sessionId, CancellationToken cancellationToken = default);

    Task<bool> IsMealTypeInSessionAsync(int mealTypeId, int mealSessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealEnumLookupDto>> GetStudentOrderTypesAsync(CancellationToken cancellationToken = default);
}
