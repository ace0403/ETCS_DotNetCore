using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;

namespace ETCS.Admin.Infrastructure.Reports;

public static class MealOrderExcelExporter
{
    private static readonly string[] OldHeaders =
    [
        "Meal Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Section",
        "Payment Status",
        "Meal Type",
        "Choice",
        "Day",
        "Items",
        "Order Date"
    ];

    private static readonly string[] NewHeaders =
    [
        "Meal Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Section",
        "Payment Status",
        "Meal Session",
        "Meal Type",
        "Choice",
        "Day",
        "Items",
        "Order Date"
    ];

    public static byte[] ExportOld(IReadOnlyList<MealOrderReportRowDto> rows) =>
        Export(rows, OldHeaders, includeMealSession: false);

    public static byte[] ExportNew(IReadOnlyList<MealOrderReportRowDto> rows) =>
        Export(rows, NewHeaders, includeMealSession: true);

    private static byte[] Export(
        IReadOnlyList<MealOrderReportRowDto> rows,
        string[] headers,
        bool includeMealSession)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("MealOrders");

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
            worksheet.Cell(excelRow, col++).Value = row.DeliveryDate;
            worksheet.Cell(excelRow, col++).Value = row.StudCode;
            worksheet.Cell(excelRow, col++).Value = row.StudFullName;
            worksheet.Cell(excelRow, col++).Value = row.StudStd;
            worksheet.Cell(excelRow, col++).Value = row.StudDiv;
            worksheet.Cell(excelRow, col++).Value = row.PaymentStatus;
            if (includeMealSession)
            {
                worksheet.Cell(excelRow, col++).Value = row.MealSession;
            }

            worksheet.Cell(excelRow, col++).Value = row.Category;
            worksheet.Cell(excelRow, col++).Value = row.Choice;
            worksheet.Cell(excelRow, col++).Value = row.Day;
            worksheet.Cell(excelRow, col++).Value = row.Items;
            worksheet.Cell(excelRow, col).Value = row.OrderDate;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"MealOrderSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
}
