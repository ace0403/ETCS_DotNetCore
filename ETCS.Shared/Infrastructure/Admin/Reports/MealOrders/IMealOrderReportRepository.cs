namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;

public interface IMealOrderReportRepository
{
    Task<IReadOnlyList<MealOrderSchoolLookupDto>> GetSchoolsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealOrderReportRowDto>> GetOrdersAsync(
        MealOrderReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<MealOrderReportPagedResult> GetOrdersPagedAsync(
        MealOrderReportListRequest request,
        CancellationToken cancellationToken = default);
}
