namespace ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;

public interface ICanteenTransactionReportRepository
{
    Task<IReadOnlyList<SchoolCodeLookupDto>> GetSchoolsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TerminalLookupDto>> GetBranchesAsync(string? schoolCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CanteenTransactionReportRowDto>> GetTransactionsAsync(
        CanteenTransactionReportFilter filter,
        CancellationToken cancellationToken = default);
    Task<CanteenTransactionReportPagedResult> GetTransactionsPagedAsync(
        CanteenTransactionReportListRequest request,
        CancellationToken cancellationToken = default);
}
