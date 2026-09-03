using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Master.Guardians;

namespace ETCS.Admin.Infrastructure.Master;

public static class GuardianMasterExcelExporter
{
    private static readonly string[] Headers =
    [
        "Name",
        "Email",
        "Mobile",
        "Username",
        "Active"
    ];

    public static byte[] Export(IReadOnlyList<GuardianListItemDto> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Parents");

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
            worksheet.Cell(excelRow, 1).Value = row.Name;
            worksheet.Cell(excelRow, 2).Value = row.Email;
            worksheet.Cell(excelRow, 3).Value = row.MobileNo ?? string.Empty;
            worksheet.Cell(excelRow, 4).Value = row.Username ?? string.Empty;
            worksheet.Cell(excelRow, 5).Value = row.IsActive ? "Yes" : "No";
        }

        worksheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public static string BuildFileName() =>
        $"Parents_{DateTime.Now:MMddyy}.xlsx";
}
