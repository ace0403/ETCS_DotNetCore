using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;

namespace ETCS.Admin.Infrastructure.Reports;

public static class TerminalSalesSummaryExcelExporter
{
    private static readonly string[] Headers =
    [
        "Terminal Code",
        "Terminal Name",
        "Date",
        "Students Count",
        "Student-Card Purchase",
        "Cash Purchase",
        "Credit Card Purchase",
        "Student-Card Manual Topup",
        "Student-Card Undo Topup",
        "Online Student-Card Topup",
        "Undo Cash Purchase"
    ];

    public static byte[] Export(IReadOnlyList<TerminalSalesSummaryReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("SalesSummary");

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
            worksheet.Cell(excelRow, 1).Value = row.TerminalCode;
            worksheet.Cell(excelRow, 2).Value = row.TerminalName;
            worksheet.Cell(excelRow, 3).Value = row.Date;
            worksheet.Cell(excelRow, 4).Value = row.StudentsCount;
            worksheet.Cell(excelRow, 5).Value = row.StudentCardPurchase;
            worksheet.Cell(excelRow, 6).Value = row.CashPurchase;
            worksheet.Cell(excelRow, 7).Value = row.CreditCardPurchase;
            worksheet.Cell(excelRow, 8).Value = row.StudentCardManualTopup;
            worksheet.Cell(excelRow, 9).Value = row.StudentCardUndoTopup;
            worksheet.Cell(excelRow, 10).Value = row.OnlineStudentCardTopup;
            worksheet.Cell(excelRow, 11).Value = row.UndoCashPurchase;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"SalesSummary_{DateTime.Now:MMddyy}.xlsx";
}
