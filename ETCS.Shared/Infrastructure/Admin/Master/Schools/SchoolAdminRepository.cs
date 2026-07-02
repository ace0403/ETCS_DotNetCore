using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public sealed class SchoolAdminRepository : ISchoolAdminRepository
{
    private const string SelectSql = """
        SELECT
            s.SchoolId AS Id,
            LTRIM(RTRIM(ISNULL(s.SchoolName, ''))) AS Name,
            LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) AS Code,
            LTRIM(RTRIM(ISNULL(c.CountryName, ''))) AS CountryName,
            CAST(ISNULL(s.MinimumTopup, 0) AS decimal(18,2)) AS MinimumTopupAmount,
            CAST(ISNULL(s.EmailAlterts, 0) AS bit) AS HasEmailNotification
        """;

    private const string FromSql = """
        FROM SchoolInfo s
        LEFT JOIN CountryInfo c ON c.CountryId = s.CountryId
        """;

    private const string SearchFilterSql = """
        LTRIM(RTRIM(ISNULL(s.SchoolName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(c.CountryName, ''))) LIKE '%' + @Search + '%'
        OR CAST(s.CountryId AS varchar(20)) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "s.SchoolId",
        ["Name"] = "s.SchoolName",
        ["Code"] = "s.Schoolcode",
        ["CountryName"] = "c.CountryName",
        ["MinimumTopupAmount"] = "s.MinimumTopup",
        ["HasEmailNotification"] = "s.EmailAlterts"
    };

    private const string GetSql = """
        SELECT TOP (1)
            s.SchoolId AS Id,
            LTRIM(RTRIM(ISNULL(s.SchoolName, ''))) AS Name,
            LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) AS Code,
            s.CountryId,
            CAST(ISNULL(s.MinimumTopup, 0) AS decimal(18,2)) AS MinimumTopupAmount,
            CAST(ISNULL(s.EmailAlterts, 0) AS bit) AS HasEmailNotification,
            LTRIM(RTRIM(ISNULL(s.SchoolLogo, ''))) AS LogoFileName,
            LTRIM(RTRIM(ISNULL(s.pdfpath, ''))) AS PdfPath
        FROM SchoolInfo s
        WHERE s.SchoolId = @Id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public SchoolAdminRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DataTableResponse<SchoolListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<SchoolListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            request.SchoolId is > 0 ? "s.SchoolId = @SchoolId" : null,
            SearchFilterSql,
            SortColumns,
            "s.SchoolName",
            request,
            request.SchoolId is > 0 ? new { SchoolId = request.SchoolId.Value } : null,
            cancellationToken: cancellationToken);
    }

    public async Task<SchoolSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<SchoolSaveRequest>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SchoolCountryLookupDto>> CountryLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<SchoolCountryLookupDto>(
            new CommandDefinition(
                """
                SELECT CountryId AS Id, LTRIM(RTRIM(ISNULL(CountryName, ''))) AS Name
                FROM CountryInfo
                ORDER BY CountryName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AdminOperationResult> SaveAsync(SchoolSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        if (request.Id > 0)
        {
            const string updateSql = """
                UPDATE SchoolInfo
                SET SchoolName = @Name,
                    Schoolcode = @Code,
                    CountryId = @CountryId,
                    MinimumTopup = @MinimumTopupAmount,
                    EmailAlterts = @HasEmailNotification,
                    SchoolLogo = CASE WHEN @LogoFileName IS NOT NULL AND @LogoFileName <> '' THEN @LogoFileName ELSE SchoolLogo END,
                    pdfpath = CASE WHEN @PdfPath IS NOT NULL AND @PdfPath <> '' THEN @PdfPath ELSE pdfpath END
                WHERE SchoolId = @Id;
                """;
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(updateSql, request, cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("School updated successfully.")
                : AdminOperationResult.Fail("School was not updated.");
        }

        const string insertSql = """
            INSERT INTO SchoolInfo (CountryId, Schoolcode, SchoolName, SchoolLogo, MinimumTopup, pdfpath, EmailAlterts)
            VALUES (@CountryId, @Code, @Name, ISNULL(@LogoFileName, ''), @MinimumTopupAmount, ISNULL(@PdfPath, ''), @HasEmailNotification);

            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        var newId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(insertSql, request, cancellationToken: cancellationToken));
        request.Id = newId;
        return newId > 0
            ? AdminOperationResult.Ok("School added successfully.")
            : AdminOperationResult.Fail("School was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        try
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM SchoolInfo WHERE SchoolId = @Id;",
                    new { Id = id },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Record deleted successfully.")
                : AdminOperationResult.Fail("Record was not deleted.");
        }
        catch
        {
            return AdminOperationResult.Fail("Record could not be deleted. It may be in use.");
        }
    }
}
