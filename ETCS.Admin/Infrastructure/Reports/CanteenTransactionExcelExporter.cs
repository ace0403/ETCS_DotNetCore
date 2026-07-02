using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;

namespace ETCS.Admin.Infrastructure.Reports;

public static class CanteenTransactionExcelExporter
{
    private static readonly string[] Headers =
    [
        "Sr. No.",
        "Date",
        "Student Card Number",
        "Student Name",
        "Item Name",
        "Price",
        "Quantity",
        "Bill amount",
        "Current Balance",
        "Branch"
    ];

    public static byte[] Export(IReadOnlyList<CanteenTransactionReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("CanteenTransactions");

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
            worksheet.Cell(excelRow, 1).Value = i + 1;
            worksheet.Cell(excelRow, 2).Value = row.DateTime?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
            worksheet.Cell(excelRow, 3).Value = row.StudCode;
            worksheet.Cell(excelRow, 4).Value = row.StudFirstName;
            worksheet.Cell(excelRow, 5).Value = row.TransactionType;
            worksheet.Cell(excelRow, 6).Value = row.Price;
            worksheet.Cell(excelRow, 7).Value = row.Quantity;
            worksheet.Cell(excelRow, 8).Value = row.Amount;
            worksheet.Cell(excelRow, 9).Value = row.BalPrepaid;
            worksheet.Cell(excelRow, 10).Value = row.Location;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"CanteenTransactions_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
}
