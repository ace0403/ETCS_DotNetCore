using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

namespace ETCS.Admin.Infrastructure.Reports;

public static class MealOrderPaymentExcelExporter
{
    private static readonly string[] OldHeaders =
    [
        "Order Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Payment Status",
        "Transaction Id",
        "Amount",
        "Meal Date",
        "Day",
        "Items"
    ];

    private static readonly string[] NewHeaders =
    [
        "Order Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Payment Status",
        "Meal Session",
        "Transaction Id",
        "Amount",
        "Meal Date",
        "Day",
        "Items"
    ];

    public static byte[] ExportOld(IReadOnlyList<MealOrderPaymentReportRowDto> rows) =>
        Export(rows, OldHeaders, includeMealSession: false);

    public static byte[] ExportNew(IReadOnlyList<MealOrderPaymentReportRowDto> rows) =>
        Export(rows, NewHeaders, includeMealSession: true);

    private static byte[] Export(
        IReadOnlyList<MealOrderPaymentReportRowDto> rows,
        string[] headers,
        bool includeMealSession)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("MealOrderPayments");

        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var col = 1;
            worksheet.Cell(excelRow, col++).Value = row.OrderDate;
            worksheet.Cell(excelRow, col++).Value = row.StudCode;
            worksheet.Cell(excelRow, col++).Value = row.StudFullName;
            worksheet.Cell(excelRow, col++).Value = row.StudStd;
            worksheet.Cell(excelRow, col++).Value = row.PaymentStatus;
            if (includeMealSession)
            {
                worksheet.Cell(excelRow, col++).Value = row.MealSession;
            }

            worksheet.Cell(excelRow, col++).Value = row.TransactionId;
            var amountCell = worksheet.Cell(excelRow, col++);
            amountCell.Value = row.Amount;
            amountCell.Style.NumberFormat.Format = "0.00";
            worksheet.Cell(excelRow, col++).Value = row.DeliveryDate;
            worksheet.Cell(excelRow, col++).Value = row.Day;
            worksheet.Cell(excelRow, col).Value = row.Items;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"MealOrderPaymentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
}
