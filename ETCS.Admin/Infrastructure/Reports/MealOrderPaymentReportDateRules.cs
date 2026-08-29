using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Admin.Infrastructure.Reports;

public sealed class MealOrderPaymentReportDateRules
{
    private readonly AdminOptions _options;

    public MealOrderPaymentReportDateRules(IOptions<AdminOptions> options)
    {
        _options = options.Value;
    }

    public DateTime? CutoverDate =>
        DateTime.TryParse(_options.MealOrderReportCutoverDate, out var parsed)
            ? parsed.Date
            : null;

    public string? CutoverDateIso => CutoverDate?.ToString("yyyy-MM-dd");

    public (string StartDate, string EndDate) GetLegacyDefaultRange()
    {
        var cutover = CutoverDate ?? DateTime.Today;
        var end = DateTime.Today <= cutover ? DateTime.Today : cutover;
        var start = end.AddDays(-1);
        if (start > cutover)
        {
            start = cutover;
        }

        return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
    }

    public (string StartDate, string EndDate) GetNewDefaultRange()
    {
        var cutover = CutoverDate ?? DateTime.Today;
        var today = DateTime.Today;
        var end = today >= cutover ? today : cutover;
        var start = end.AddDays(-1);
        if (start < cutover)
        {
            start = cutover;
        }

        return (start.ToString("yyyy-MM-dd"), end.ToString("yyyy-MM-dd"));
    }
}
