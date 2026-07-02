namespace ETCS.Shared.Infrastructure.Meals;

public interface IMealRepository
{
    Task<IReadOnlyList<MealItemDto>> GetMealItemsForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealPackageDto>> GetMealPackagesForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default);
}
