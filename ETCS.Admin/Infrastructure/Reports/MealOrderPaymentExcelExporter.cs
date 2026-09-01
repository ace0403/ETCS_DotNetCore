using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

namespace ETCS.Admin.Infrastructure.Reports;

public static class MealOrderPaymentExcelExporter
{
    private static readonly string[] OldHeaders =
    [
        "Transaction Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Transaction Id",
        "Transaction Type",
        "Package",
        "Amount",
        "School Name"
    ];

    private static readonly string[] NewHeaders =
    [
        "Transaction Date",
        "Student Card No.",
        "Student Name",
        "Grade",
        "Transaction Id",
        "Transaction Type",
        "Package",
        "Amount",
        "School Name"
    ];

    public static byte[] ExportOld(IReadOnlyList<MealOrderPaymentReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("MealOrderPayments");
        WriteHeaders(worksheet, OldHeaders);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var col = 1;
            worksheet.Cell(excelRow, col++).Value = row.OrderDate;
            worksheet.Cell(excelRow, col++).Value = row.StudCode;
            worksheet.Cell(excelRow, col++).Value = row.StudFullName;
            worksheet.Cell(excelRow, col++).Value = row.StudStd;
            worksheet.Cell(excelRow, col++).Value = row.TransactionId;
            worksheet.Cell(excelRow, col++).Value = row.TransactionType;
            worksheet.Cell(excelRow, col++).Value = row.Package;
            var amountCell = worksheet.Cell(excelRow, col++);
            amountCell.Value = row.Amount;
            amountCell.Style.NumberFormat.Format = "0.00";
            worksheet.Cell(excelRow, col).Value = row.SchoolName;
        }

        worksheet.Columns().AdjustToContents();
        return SaveWorkbook(workbook);
    }

    public static byte[] ExportNew(IReadOnlyList<MealOrderPaymentReportRowDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("MealOrderPayments");
        WriteHeaders(worksheet, NewHeaders);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            var col = 1;
            worksheet.Cell(excelRow, col++).Value = row.OrderDate;
            worksheet.Cell(excelRow, col++).Value = row.StudCode;
            worksheet.Cell(excelRow, col++).Value = row.StudFullName;
            worksheet.Cell(excelRow, col++).Value = row.StudStd;
            worksheet.Cell(excelRow, col++).Value = row.TransactionId;
            worksheet.Cell(excelRow, col++).Value = row.TransactionType;
            worksheet.Cell(excelRow, col++).Value = row.Package;
            var amountCell = worksheet.Cell(excelRow, col++);
            amountCell.Value = row.Amount;
            amountCell.Style.NumberFormat.Format = "0.00";
            worksheet.Cell(excelRow, col).Value = row.SchoolName;
        }

        worksheet.Columns().AdjustToContents();
        return SaveWorkbook(workbook);
    }

    private static void WriteHeaders(IXLWorksheet worksheet, string[] headers)
    {
        for (var col = 0; col < headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = headers[col];
            cell.Style.Font.Bold = true;
        }
    }

    private static byte[] SaveWorkbook(XLWorkbook workbook)
    {
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"MealOrderPaymentSummary_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";
}
