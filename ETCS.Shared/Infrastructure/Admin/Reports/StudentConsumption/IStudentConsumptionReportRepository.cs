namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

public interface IStudentConsumptionReportRepository
{
    Task<StudentConsumptionReportResult> GetReportAsync(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StudentConsumptionStudentLookupDto>> SearchStudentsAsync(
        string term,
        IReadOnlyList<int>? scopedSchoolIds,
        int maxResults = 20,
        CancellationToken cancellationToken = default);
}
