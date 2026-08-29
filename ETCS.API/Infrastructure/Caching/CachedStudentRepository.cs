using ETCS.Shared.Infrastructure.Students;
using Microsoft.Extensions.Caching.Memory;

namespace ETCS.API.Infrastructure.Caching;

public sealed class CachedStudentRepository : IStudentRepository
{
    private static readonly TimeSpan GuardianDetailCacheTtl = TimeSpan.FromMinutes(3);

    private readonly StudentRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedStudentRepository(StudentRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public Task<List<StudentSummaryDto>> GetStudentSummaryAsync(
        string? studId,
        int grdId,
        CancellationToken cancellationToken)
        => _inner.GetStudentSummaryAsync(studId, grdId, cancellationToken);

    public Task<List<StudentListingDto>> GetStudentsByGuardianAsync(
        int guardianId,
        string? customerId,
        CancellationToken cancellationToken)
        => _inner.GetStudentsByGuardianAsync(guardianId, customerId, cancellationToken);

    public Task<IReadOnlyList<StudentBasicListItemDto>> GetStudentBasicListByGuardianAsync(
        int guardianId,
        CancellationToken cancellationToken)
        => _inner.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);

    public Task<int?> GetStudentSchoolIdAsync(int studentId, CancellationToken cancellationToken = default)
        => _inner.GetStudentSchoolIdAsync(studentId, cancellationToken);

    public Task<bool?> GetSchoolEmailAlertsEnabledAsync(int schoolId, CancellationToken cancellationToken = default)
        => _inner.GetSchoolEmailAlertsEnabledAsync(schoolId, cancellationToken);

    public Task<decimal?> GetStudentMinimumTopupAsync(int studentId, CancellationToken cancellationToken = default)
        => _inner.GetStudentMinimumTopupAsync(studentId, cancellationToken);

    public Task<StudentCardBalanceMetaDto?> GetStudentCardBalanceMetaAsync(
        int studentId,
        CancellationToken cancellationToken = default)
        => _inner.GetStudentCardBalanceMetaAsync(studentId, cancellationToken);

    public Task<string?> GetSchoolLogoFileNameByNameAsync(
        string schoolName,
        CancellationToken cancellationToken = default)
        => _inner.GetSchoolLogoFileNameByNameAsync(schoolName, cancellationToken);

    public async Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByStudentIdAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        var key = $"guardian-detail:{studentId.Trim()}";
        if (_cache.TryGetValue(key, out StudentGuardianBasicDetailDto? cached))
        {
            return cached;
        }

        var detail = await _inner.GetGuardianBasicDetailByStudentIdAsync(studentId, cancellationToken);
        if (detail is not null)
        {
            _cache.Set(key, detail, GuardianDetailCacheTtl);
        }

        return detail;
    }

    public Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
        => _inner.GetGuardianBasicDetailByCustomerIdAsync(customerId, cancellationToken);

    public Task<StudentIdentityByCustomerDto?> GetStudentIdentityByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
        => _inner.GetStudentIdentityByCustomerIdAsync(customerId, cancellationToken);

    public Task<decimal> GetPrepaidBalanceByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
        => _inner.GetPrepaidBalanceByCustomerIdAsync(customerId, cancellationToken);

    public Task<IReadOnlyList<GradeListItemDto>> GetAllGradesAsync(CancellationToken cancellationToken)
        => _inner.GetAllGradesAsync(cancellationToken);

    public Task<IReadOnlyList<SchoolListItemDto>> GetSchoolsByCountryAsync(
        int countryId,
        string? schoolId,
        CancellationToken cancellationToken)
        => _inner.GetSchoolsByCountryAsync(countryId, schoolId, cancellationToken);

    public Task SaveStudentAsync(UpsertStudentRequest request, bool isInsert, CancellationToken cancellationToken)
        => _inner.SaveStudentAsync(request, isInsert, cancellationToken);
}
