using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;

namespace ETCS.Admin.Infrastructure.Reports;

public static class MealOrderExcelExporter
{
    private static readonly string[] Headers =
    [
        "Order Date",
        "Student Card No.",
        "Grade",
        "Section",
        "Student Name",
        "Payment Status",
        "Meal Type",
        "Choice",
        "Meal Date",
        "Day",
        "Items"
    ];

    public static byte[] Export(IReadOnlyList<MealOrderReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("MealOrders");

        for (var col = 0; col < Headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = Headers[col];
            cell.Style.Font.Bold = true;
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            worksheet.Cell(excelRow, 1).Value = row.OrderDate;
            worksheet.Cell(excelRow, 2).Value = row.StudCode;
            worksheet.Cell(excelRow, 3).Value = row.StudStd;
            worksheet.Cell(excelRow, 4).Value = row.StudDiv;
            worksheet.Cell(excelRow, 5).Value = row.StudFullName;
            worksheet.Cell(excelRow, 6).Value = row.PaymentStatus;
            worksheet.Cell(excelRow, 7).Value = row.Category;
            worksheet.Cell(excelRow, 8).Value = row.Choice;
            worksheet.Cell(excelRow, 9).Value = row.DeliveryDate;
            worksheet.Cell(excelRow, 10).Value = row.Day;
            worksheet.Cell(excelRow, 11).Value = row.Items;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"MealOrderSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
}
