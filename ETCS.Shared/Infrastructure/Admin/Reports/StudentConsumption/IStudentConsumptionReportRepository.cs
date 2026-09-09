namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

public interface IStudentConsumptionReportRepository
{
    Task<StudentConsumptionReportPagedData> GetReportPagedAsync(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        int studentSkip,
        int studentTake,
        CancellationToken cancellationToken = default);

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
