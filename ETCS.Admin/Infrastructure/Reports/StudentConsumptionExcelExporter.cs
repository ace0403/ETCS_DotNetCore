using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

namespace ETCS.Admin.Infrastructure.Reports;

public static class StudentConsumptionExcelExporter
{
    private static readonly string[] Headers =
    [
        "Student Name",
        "Code",
        "Customer Details",
        "Trans Date",
        "Debit",
        "Credit",
        "Amount"
    ];

    public static byte[] Export(StudentConsumptionReportResult report)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Student Consumption");

        for (var col = 0; col < Headers.Length; col++)
        {
            var cell = worksheet.Cell(1, col + 1);
            cell.Value = Headers[col];
            cell.Style.Font.Bold = true;
        }

        var excelRow = 2;
        var blocks = SplitIntoBlocks(report.Rows);

        foreach (var block in blocks)
        {
            var blockStartRow = excelRow;

            for (var i = 0; i < block.Count; i++)
            {
                var row = block[i];
                WriteRow(worksheet, excelRow, row);
                excelRow++;
            }

            if (block.Count > 1)
            {
                var blockEndRow = excelRow - 1;
                worksheet.Range(blockStartRow, 1, blockEndRow, 1).Merge();
                worksheet.Range(blockStartRow, 2, blockEndRow, 2).Merge();
                worksheet.Cell(blockStartRow, 1).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                worksheet.Cell(blockStartRow, 2).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;

                var detailEndRow = blockEndRow;
                if (block[^1].RowKind == StudentConsumptionRowKind.Total)
                {
                    detailEndRow = blockEndRow - 1;
                }

                if (detailEndRow > blockStartRow)
                {
                    worksheet.Range(blockStartRow, 3, detailEndRow, 3).Merge();
                    worksheet.Cell(blockStartRow, 3).Style.Alignment.Vertical = XLAlignmentVerticalValues.Top;
                }
            }
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"StudentConsumption_{DateTime.Now:MMddyy}.xlsx";

    private static void WriteRow(IXLWorksheet worksheet, int excelRow, StudentConsumptionReportRowDto row)
    {
        worksheet.Cell(excelRow, 1).Value = row.StudentName;
        worksheet.Cell(excelRow, 2).Value = row.StudCode;
        worksheet.Cell(excelRow, 3).Value = row.CustomerDetails;
        worksheet.Cell(excelRow, 4).Value = row.TransDate;
        worksheet.Cell(excelRow, 5).Value = row.Debit ?? 0m;
        worksheet.Cell(excelRow, 6).Value = row.Credit ?? 0m;
        worksheet.Cell(excelRow, 7).Value = row.Amount;

        worksheet.Cell(excelRow, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Cell(excelRow, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        worksheet.Cell(excelRow, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

        if (row.RowKind == StudentConsumptionRowKind.Total)
        {
            for (var col = 1; col <= Headers.Length; col++)
            {
                worksheet.Cell(excelRow, col).Style.Font.Bold = true;
            }
        }
    }

    private static List<List<StudentConsumptionReportRowDto>> SplitIntoBlocks(
        IReadOnlyList<StudentConsumptionReportRowDto> rows)
    {
        var blocks = new List<List<StudentConsumptionReportRowDto>>();
        var current = new List<StudentConsumptionReportRowDto>();

        foreach (var row in rows)
        {
            current.Add(row);
            if (row.RowKind == StudentConsumptionRowKind.Total)
            {
                blocks.Add(current);
                current = [];
            }
        }

        if (current.Count > 0)
        {
            blocks.Add(current);
        }

        return blocks;
    }
}
