using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

namespace ETCS.Shared.Application.Students;

public static class StudentOrderTypeValidation
{
    public const string SchoolCapMessage = "One or more order types are not allowed for the selected school.";

    public static string? ValidateAgainstSchool(
        IReadOnlyList<int> schoolAllowedIds,
        IReadOnlyList<int>? studentOrderTypeIds)
    {
        if (schoolAllowedIds.Count == 0 || studentOrderTypeIds is null or { Count: 0 })
        {
            return null;
        }

        var hasInvalid = studentOrderTypeIds
            .Distinct()
            .Any(id => !schoolAllowedIds.Contains(id));

        return hasInvalid ? SchoolCapMessage : null;
    }

    public static IReadOnlyList<int> NormalizeMealItemChannelsForEdit(IReadOnlyList<int> storedIds) =>
        storedIds.Count == 0
            ? [MealItemChannelOptionIds.DefaultWhenMissing]
            : storedIds;
}
