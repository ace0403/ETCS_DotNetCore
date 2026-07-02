namespace ETCS.Shared.Infrastructure.Students;

public interface IStudentRepository
{
    Task<List<StudentSummaryDto>> GetStudentSummaryAsync(
        string? studId,
        int grdId,
        CancellationToken cancellationToken);

    Task<List<StudentListingDto>> GetStudentsByGuardianAsync(
        int guardianId,
        string? customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<StudentBasicListItemDto>> GetStudentBasicListByGuardianAsync(
        int guardianId,
        CancellationToken cancellationToken);

    Task<int?> GetStudentSchoolIdAsync(int studentId, CancellationToken cancellationToken = default);

    Task<bool?> GetSchoolEmailAlertsEnabledAsync(int schoolId, CancellationToken cancellationToken = default);

    Task<decimal?> GetStudentMinimumTopupAsync(int studentId, CancellationToken cancellationToken = default);

    Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByStudentIdAsync(
        string studentId,
        CancellationToken cancellationToken);

    Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<GradeListItemDto>> GetAllGradesAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<SchoolListItemDto>> GetSchoolsByCountryAsync(
        int countryId,
        string? schoolId,
        CancellationToken cancellationToken);

    Task SaveStudentAsync(UpsertStudentRequest request, bool isInsert, CancellationToken cancellationToken);
}
