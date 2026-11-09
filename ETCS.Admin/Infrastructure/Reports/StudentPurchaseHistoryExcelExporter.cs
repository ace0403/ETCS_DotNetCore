using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.StudentPurchaseHistory;

namespace ETCS.Admin.Infrastructure.Reports;

public static class StudentPurchaseHistoryExcelExporter
{
    private static readonly string[] Headers =
    [
        "Student Name",
        "Student Code",
        "Grade",
        "Source",
        "Item Name",
        "Qty",
        "Amount",
        "Date Time",
        "Order / Ref"
    ];

    public static byte[] Export(IReadOnlyList<StudentPurchaseHistoryReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("PurchaseHistory");

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
            worksheet.Cell(excelRow, 1).Value = row.StudentName;
            worksheet.Cell(excelRow, 2).Value = row.StudentCode;
            worksheet.Cell(excelRow, 3).Value = row.Grade;
            worksheet.Cell(excelRow, 4).Value = row.Source;
            worksheet.Cell(excelRow, 5).Value = row.ItemName;
            worksheet.Cell(excelRow, 6).Value = row.Quantity;
            worksheet.Cell(excelRow, 7).Value = row.Amount;
            worksheet.Cell(excelRow, 8).Value = row.PurchaseDateTime;
            worksheet.Cell(excelRow, 9).Value = row.Reference;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"StudentPurchaseHistory_{DateTime.Now:MMddyy}.xlsx";
}
