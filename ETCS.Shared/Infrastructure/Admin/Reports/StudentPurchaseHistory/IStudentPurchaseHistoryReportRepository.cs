namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentPurchaseHistory;

public interface IStudentPurchaseHistoryReportRepository
{
    Task<IReadOnlyList<StudentPurchaseHistoryReportRowDto>> GetRowsAsync(
        StudentPurchaseHistoryReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<StudentPurchaseHistoryReportPagedResult> GetRowsPagedAsync(
        StudentPurchaseHistoryReportListRequest request,
        CancellationToken cancellationToken = default);
}
