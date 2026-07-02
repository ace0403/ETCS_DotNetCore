namespace ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;

public interface IAdminTransactionReportRepository
{
    Task<IReadOnlyList<AdminTransactionReportRowDto>> GetTransactionsAsync(
        AdminTransactionReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<AdminTransactionReportPagedResult> GetTransactionsPagedAsync(
        AdminTransactionReportListRequest request,
        CancellationToken cancellationToken = default);
}
