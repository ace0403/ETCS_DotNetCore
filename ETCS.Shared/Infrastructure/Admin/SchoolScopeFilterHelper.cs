using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin;

public static class SchoolScopeFilterHelper
{
    public static (string? FilterSql, object? Parameters) BuildSchoolIdFilter(
        DataTableRequest request,
        string columnExpression)
    {
        if (request.ScopedSchoolIds is { Count: > 0 })
        {
            return ($"{columnExpression} IN @ScopedSchoolIds", new { ScopedSchoolIds = request.ScopedSchoolIds });
        }

        if (request.SchoolId is > 0)
        {
            return ($"{columnExpression} = @SchoolId", new { SchoolId = request.SchoolId.Value });
        }

        return (null, null);
    }

    public static string ToCsv(IReadOnlyList<string> values) =>
        values.Count == 0 ? string.Empty : string.Join(",", values);

    public static string ToCsv(IReadOnlyList<int> values) =>
        values.Count == 0 ? string.Empty : string.Join(",", values);
}
