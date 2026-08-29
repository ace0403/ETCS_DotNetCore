using System.Globalization;
using ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

namespace ETCS.Admin.Infrastructure.Reports;

public static class MealOrderPaymentAdminTransactionMapper
{
    public const string MealPlanTransactionType = "9001";

    public static AdminTransactionReportListRequest ToAdminListRequest(
        MealOrderPaymentReportListRequest request) =>
        new()
        {
            Draw = request.Draw,
            Start = request.Start,
            Length = request.Length,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            SchoolCode = request.SchoolId,
            TransactionType = MealPlanTransactionType
        };

    public static AdminTransactionReportFilter ToAdminFilter(MealOrderPaymentReportFilter filter) =>
        new()
        {
            StartDate = filter.StartDate,
            EndDate = filter.EndDate,
            SchoolCode = filter.SchoolId,
            TransactionType = MealPlanTransactionType
        };

    public static MealOrderPaymentReportRowDto ToPaymentRow(AdminTransactionReportRowDto row)
    {
        SplitClass(row.Class, out var studStd, out var studDiv);

        return new MealOrderPaymentReportRowDto
        {
            OrderDate = FormatDate(row.DateTime),
            StudCode = row.StudentId,
            StudStd = studStd,
            StudDiv = studDiv,
            StudFullName = row.Name,
            TransactionType = row.TransactionType,
            TransactionId = row.TransactionId,
            Amount = row.Amount,
            SchoolName = row.Terminal
        };
    }

    public static IReadOnlyList<MealOrderPaymentReportRowDto> ToPaymentRows(
        IEnumerable<AdminTransactionReportRowDto> rows) =>
        rows.Select(ToPaymentRow).ToList();

    private static void SplitClass(string? classValue, out string studStd, out string studDiv)
    {
        studStd = string.Empty;
        studDiv = string.Empty;

        if (string.IsNullOrWhiteSpace(classValue))
        {
            return;
        }

        var trimmed = classValue.Trim();
        var dashIndex = trimmed.IndexOf('-');
        if (dashIndex < 0)
        {
            studStd = trimmed;
            return;
        }

        studStd = trimmed[..dashIndex].Trim();
        studDiv = trimmed[(dashIndex + 1)..].Trim();
    }

    private static string FormatDate(DateTime? value) =>
        value.HasValue
            ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : string.Empty;
}
