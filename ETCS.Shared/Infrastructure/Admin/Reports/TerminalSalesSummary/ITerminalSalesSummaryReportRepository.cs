namespace ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;

public interface ITerminalSalesSummaryReportRepository
{
    Task<IReadOnlyList<TerminalSalesSummaryReportRowDto>> GetSummaryAsync(
        TerminalSalesSummaryReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<TerminalSalesSummaryReportPagedResult> GetSummaryPagedAsync(
        TerminalSalesSummaryReportListRequest request,
        CancellationToken cancellationToken = default);
}
