namespace ETCS.Shared.Infrastructure.Meals.Menu;

public interface IMealMenuComposer
{
    Task<MealMenuResponse> ComposeMenuAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealSchoolDayDto>> ComposeSchoolDaysAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateInclusive,
        CancellationToken cancellationToken = default);
}
