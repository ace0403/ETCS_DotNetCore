using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Master.Students;

namespace ETCS.Admin.Infrastructure.Master;

public static class StudentMasterExcelExporter
{
    private static readonly string[] Headers =
    [
        "Student Id No",
        "Name",
        "School",
        "Standard",
        "Parent",
        "Balance",
        "Created Date"
    ];

    public static byte[] Export(IReadOnlyList<StudentAdminListItemDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Students");

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
            worksheet.Cell(excelRow, 1).Value = row.StudCode ?? string.Empty;
            worksheet.Cell(excelRow, 2).Value = row.Name;
            worksheet.Cell(excelRow, 3).Value = row.SchoolName ?? string.Empty;
            worksheet.Cell(excelRow, 4).Value = row.Grade ?? string.Empty;
            worksheet.Cell(excelRow, 5).Value = row.GuardianName ?? string.Empty;
            worksheet.Cell(excelRow, 6).Value = row.Balance;
            worksheet.Cell(excelRow, 7).Value = row.CreatedAt?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"Students_{DateTime.Now:MMddyy}.xlsx";
}
