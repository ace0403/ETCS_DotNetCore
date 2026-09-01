using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;
using static ETCS.Shared.Infrastructure.Admin.SchoolScopeFilterHelper;

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
    private readonly ISchoolOrderTypeAdminRepository _orderTypeRepository;
    private readonly ISchoolGradeOrderTypeAdminRepository _gradeOrderTypeRepository;

    public SchoolAdminRepository(
        IDbConnectionFactory connectionFactory,
        ISchoolOrderTypeAdminRepository orderTypeRepository,
        ISchoolGradeOrderTypeAdminRepository gradeOrderTypeRepository)
    {
        _connectionFactory = connectionFactory;
        _orderTypeRepository = orderTypeRepository;
        _gradeOrderTypeRepository = gradeOrderTypeRepository;
    }

    public async Task<DataTableResponse<SchoolListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var (schoolFilterSql, schoolFilterParams) = BuildSchoolIdFilter(request, "s.SchoolId");
        return await QueryPagedAsync<SchoolListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            schoolFilterSql,
            SearchFilterSql,
            SortColumns,
            "s.SchoolName",
            request,
            schoolFilterParams,
            cancellationToken: cancellationToken);
    }

    public async Task<SchoolSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var request = await dbConnection.QuerySingleOrDefaultAsync<SchoolSaveRequest>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
        if (request is null) return null;

        request.OrderTypeIds = (await _orderTypeRepository.GetOrderTypeIdsAsync(id, cancellationToken)).ToList();
        request.GradeOrderTypeConfigs = (await _gradeOrderTypeRepository.GetConfigsAsync(id, cancellationToken)).ToList();
        return request;
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
            if (rows <= 0)
            {
                return AdminOperationResult.Fail("School was not updated.");
            }

            await _orderTypeRepository.SaveOrderTypesAsync(request.Id, request.OrderTypeIds ?? [], cancellationToken);
            await _gradeOrderTypeRepository.SaveConfigsAsync(request.Id, request.GradeOrderTypeConfigs ?? [], cancellationToken);
            return AdminOperationResult.Ok("School updated successfully.");
        }

        const string insertSql = """
            INSERT INTO SchoolInfo (CountryId, Schoolcode, SchoolName, SchoolLogo, MinimumTopup, pdfpath, EmailAlterts)
            VALUES (@CountryId, @Code, @Name, ISNULL(@LogoFileName, ''), @MinimumTopupAmount, ISNULL(@PdfPath, ''), @HasEmailNotification);

            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        var newId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(insertSql, request, cancellationToken: cancellationToken));
        request.Id = newId;
        if (newId <= 0)
        {
            return AdminOperationResult.Fail("School was not added.");
        }

        await _orderTypeRepository.SaveOrderTypesAsync(newId, request.OrderTypeIds ?? [], cancellationToken);
        await _gradeOrderTypeRepository.SaveConfigsAsync(newId, request.GradeOrderTypeConfigs ?? [], cancellationToken);
        return AdminOperationResult.Ok("School added successfully.");
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
            if (rows <= 0)
            {
                return AdminOperationResult.Fail("Record was not deleted.");
            }

            await _orderTypeRepository.DeleteOrderTypesAsync(id, cancellationToken);
            await _gradeOrderTypeRepository.DeleteConfigsAsync(id, cancellationToken);
            return AdminOperationResult.Ok("Record deleted successfully.");
        }
        catch
        {
            return AdminOperationResult.Fail("Record could not be deleted. It may be in use.");
        }
    }
}
