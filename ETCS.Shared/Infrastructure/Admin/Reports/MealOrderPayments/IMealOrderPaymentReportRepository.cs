namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

public interface IMealOrderPaymentReportRepository
{
    Task<IReadOnlyList<MealOrderPaymentSchoolLookupDto>> GetSchoolsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MealOrderPaymentReportRowDto>> GetOrdersAsync(
        MealOrderPaymentReportFilter filter,
        CancellationToken cancellationToken = default);

    Task<MealOrderPaymentReportPagedResult> GetOrdersPagedAsync(
        MealOrderPaymentReportListRequest request,
        CancellationToken cancellationToken = default);
}
