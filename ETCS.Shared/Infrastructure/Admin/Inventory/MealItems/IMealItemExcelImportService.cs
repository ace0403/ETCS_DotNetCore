namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public interface IMealItemExcelImportService
{
    Task<MealItemImportParseResult> ParseAsync(
        Stream fileStream,
        int schoolId,
        int mealSessionId,
        int mealTypeId,
        bool createMissingCategories = false,
        int? createdBy = null,
        CancellationToken cancellationToken = default);
}
