using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;

namespace ETCS.Admin.Infrastructure.Reports;

public static class AdminTransactionExcelExporter
{
    private static readonly string[] Headers =
    [
        "Date",
        "Student Id",
        "Name",
        "Class",
        "Amount",
        "VAT",
        "Terminal",
        "Transaction Type",
        "Transaction Id"
    ];

    public static byte[] Export(IReadOnlyList<AdminTransactionReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Transactions");

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
            worksheet.Cell(excelRow, 1).Value = row.DateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            worksheet.Cell(excelRow, 2).Value = row.StudentId;
            worksheet.Cell(excelRow, 3).Value = row.Name;
            worksheet.Cell(excelRow, 4).Value = row.Class;
            worksheet.Cell(excelRow, 5).Value = row.Amount;
            worksheet.Cell(excelRow, 6).Value = row.Vat;
            worksheet.Cell(excelRow, 7).Value = row.Terminal;
            worksheet.Cell(excelRow, 8).Value = row.TransactionType;
            worksheet.Cell(excelRow, 9).Value = row.TransactionId;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"Transaction_History_{DateTime.Now:MMddyy}.xlsx";
}
