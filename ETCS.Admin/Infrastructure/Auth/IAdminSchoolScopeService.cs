using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;

namespace ETCS.Admin.Infrastructure.Auth;

public interface IAdminSchoolScopeService
{
    bool IsUnrestricted { get; }

    IReadOnlyList<int> GetAllowedSchoolIds();

    Task<IReadOnlyList<string>> GetAllowedSchoolCodesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolCodeLookupDto>> FilterSchoolCodesAsync(
        IReadOnlyList<SchoolCodeLookupDto> schools,
        CancellationToken cancellationToken = default);

    void ApplyListScope(DataTableRequest request);

    void EnsureInScope(int? schoolId);

    void EnsureInScope(int schoolId);

    Task EnsureReportSchoolCodeInScopeAsync(string? schoolCode, CancellationToken cancellationToken = default);

    void EnsureReportSchoolIdInScope(string? schoolId);

    IReadOnlyList<T> FilterSchools<T>(IEnumerable<T> schools, Func<T, int> idSelector);

    IReadOnlyList<T> FilterMealOrderSchools<T>(IEnumerable<T> schools, Func<T, string> idSelector)
        where T : class;
}
