using System.Data.Common;
using Dapper;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Auth;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Master.Staff;

public sealed class StaffAdminRepository : IStaffAdminRepository
{
    private const string SelectSql = "SELECT CAST(la.Sid AS int) AS Id, LTRIM(RTRIM(CAST(la.StaffId AS varchar(50)))) AS StaffId, LTRIM(RTRIM(CAST(la.LoginName AS varchar(50)))) AS LoginName, LTRIM(RTRIM(ISNULL(la.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(la.LastName, ''))) AS Name, LTRIM(RTRIM(CAST(la.Email AS varchar(100)))) AS Email, LTRIM(RTRIM(ISNULL(ri.RoleName, ''))) AS RoleName, CAST(ISNULL(la.Enabled, 0) AS bit) AS IsActive";
    private const string FromSql = "FROM LoginAccount la LEFT JOIN RoleInfo ri ON ri.RoleID = la.RoleID";
    private const string SearchFilterSql = "LTRIM(RTRIM(CAST(la.StaffId AS varchar(50)))) LIKE '%' + @Search + '%' OR LTRIM(RTRIM(CAST(la.LoginName AS varchar(50)))) LIKE '%' + @Search + '%' OR LTRIM(RTRIM(ISNULL(la.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(la.LastName, ''))) LIKE '%' + @Search + '%' OR LTRIM(RTRIM(CAST(la.Email AS varchar(100)))) LIKE '%' + @Search + '%' OR LTRIM(RTRIM(ISNULL(ri.RoleName, ''))) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "la.Sid",
        ["StaffId"] = "la.StaffId",
        ["LoginName"] = "la.LoginName",
        ["Name"] = "la.FirstName",
        ["Email"] = "la.Email",
        ["RoleName"] = "ri.RoleName",
        ["IsActive"] = "la.Enabled"
    };

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IStaffLoginAssignmentRepository _assignmentRepository;

    public StaffAdminRepository(
        IDbConnectionFactory connectionFactory,
        IStaffLoginAssignmentRepository assignmentRepository)
    {
        _connectionFactory = connectionFactory;
        _assignmentRepository = assignmentRepository;
    }

    public async Task<DataTableResponse<StaffListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        string? baseFilterSql = null;
        object? extraParameters = null;
        if (request.ScopedSchoolIds is { Count: > 0 })
        {
            var loginAccountIds = new List<int>();
            foreach (var scopedSchoolId in request.ScopedSchoolIds)
            {
                var ids = await _assignmentRepository.GetLoginAccountIdsBySchoolAsync(
                    scopedSchoolId,
                    cancellationToken);
                loginAccountIds.AddRange(ids);
            }

            loginAccountIds = loginAccountIds.Distinct().ToList();
            baseFilterSql = loginAccountIds.Count > 0
                ? "(la.Sid IN @LoginAccountIds OR la.SchoolId IN @ScopedSchoolIds)"
                : "la.SchoolId IN @ScopedSchoolIds";
            extraParameters = loginAccountIds.Count > 0
                ? new { ScopedSchoolIds = request.ScopedSchoolIds, LoginAccountIds = loginAccountIds }
                : new { ScopedSchoolIds = request.ScopedSchoolIds };
        }
        else if (request.SchoolId is > 0)
        {
            var loginAccountIds = await _assignmentRepository.GetLoginAccountIdsBySchoolAsync(
                request.SchoolId.Value,
                cancellationToken);
            baseFilterSql = loginAccountIds.Count > 0
                ? "(la.Sid IN @LoginAccountIds OR la.SchoolId = @SchoolId)"
                : "la.SchoolId = @SchoolId";
            extraParameters = loginAccountIds.Count > 0
                ? new { SchoolId = request.SchoolId.Value, LoginAccountIds = loginAccountIds }
                : new { SchoolId = request.SchoolId.Value };
        }

        return await QueryPagedAsync<StaffListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql,
            SearchFilterSql,
            SortColumns,
            "la.FirstName",
            request,
            extraParameters,
            cancellationToken: cancellationToken);
    }

    public async Task<StaffSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var row = await dbConnection.QuerySingleOrDefaultAsync<StaffSaveRequest>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    CAST(la.Sid AS int) AS Id,
                    LTRIM(RTRIM(CAST(la.LoginName AS varchar(50)))) AS LoginName,
                    LTRIM(RTRIM(ISNULL(la.FirstName, ''))) AS FirstName,
                    LTRIM(RTRIM(ISNULL(la.LastName, ''))) AS LastName,
                    LTRIM(RTRIM(CAST(la.StaffId AS varchar(50)))) AS StaffId,
                    LTRIM(RTRIM(CAST(la.Email AS varchar(100)))) AS Email,
                    la.DOB AS DateOfBirth,
                    ISNULL(la.CountryId, 0) AS CountryId,
                    CAST(ISNULL(la.SchoolId, 0) AS int) AS SchoolId,
                    ISNULL(la.RoleID, 0) AS RoleId,
                    LTRIM(RTRIM(ISNULL(la.SecurityQue, ''))) AS SecurityQuestion,
                    LTRIM(RTRIM(ISNULL(la.SecurityAns, ''))) AS SecurityAnswer
                FROM LoginAccount la
                WHERE la.Sid = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
        if (row is null)
        {
            return null;
        }

        row.Password = null;
        row.SchoolIds = (await _assignmentRepository.GetSchoolIdsAsync(id, cancellationToken)).ToList();
        if (row.SchoolIds.Count == 0 && row.SchoolId > 0)
        {
            row.SchoolIds = [row.SchoolId];
        }

        row.RoleIds = (await _assignmentRepository.GetRoleIdsAsync(id, cancellationToken)).ToList();
        if (row.RoleIds.Count == 0 && row.RoleId > 0)
        {
            row.RoleIds = [row.RoleId];
        }

        row.DefaultRoleId = await _assignmentRepository.GetDefaultRoleIdAsync(id, cancellationToken) ?? row.RoleId;
        return row;
    }

    public async Task<IReadOnlyList<StaffRoleLookupDto>> RoleLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<StaffRoleLookupDto>(
            new CommandDefinition(
                "SELECT RoleID AS Id, LTRIM(RTRIM(ISNULL(RoleName, ''))) AS Name FROM RoleInfo ORDER BY RoleName;",
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StaffCountryLookupDto>> CountryLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<StaffCountryLookupDto>(
            new CommandDefinition(
                """
                SELECT CountryId AS Id, LTRIM(RTRIM(ISNULL(CountryName, ''))) AS Name
                FROM CountryInfo
                ORDER BY CountryName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StaffSchoolLookupDto>> SchoolLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<StaffSchoolLookupDto>(
            new CommandDefinition(
                """
                SELECT CAST(SchoolId AS int) AS Id, LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
                FROM SchoolInfo
                ORDER BY SchoolName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<StaffSchoolLookupDto>> SchoolLookupsByCountryAsync(
        int countryId,
        CancellationToken cancellationToken = default)
    {
        if (countryId <= 0) return Array.Empty<StaffSchoolLookupDto>();
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<StaffSchoolLookupDto>(
            new CommandDefinition(
                """
                SELECT CAST(SchoolId AS int) AS Id, LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
                FROM SchoolInfo
                WHERE CountryId = @CountryId
                ORDER BY SchoolName;
                """,
                new { CountryId = countryId },
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<AdminOperationResult> SaveAsync(StaffSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.LoginName))
            return AdminOperationResult.Fail("Login name is required.");

        var schoolIds = request.SchoolIds.Where(id => id > 0).Distinct().ToList();
        var roleIds = request.RoleIds.Where(id => id > 0).Distinct().ToList();
        if (roleIds.Count == 0 && request.RoleId > 0)
        {
            roleIds = [request.RoleId];
        }
        if (schoolIds.Count == 0)
            return AdminOperationResult.Fail("At least one school is required.");
        if (roleIds.Count == 0)
            return AdminOperationResult.Fail("At least one role is required.");

        var primarySchoolId = schoolIds[0];
        request.SchoolId = primarySchoolId;
        request.RoleId = request.DefaultRoleId is > 0 && roleIds.Contains(request.DefaultRoleId.Value)
            ? request.DefaultRoleId.Value
            : roleIds[0];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var passwordHash = string.IsNullOrWhiteSpace(request.Password)
            ? null
            : SecurityHelper.GetMd5Hash(request.Password);

        if (request.Id > 0)
        {
            const string updateSql = """
                UPDATE LoginAccount
                SET FirstName = @FirstName,
                    LastName = @LastName,
                    RoleID = @RoleId,
                    CountryId = @CountryId,
                    SchoolId = @SchoolId,
                    DOB = @DateOfBirth,
                    Email = @Email,
                    StaffId = @StaffId,
                    SecurityQue = @SecurityQuestion,
                    SecurityAns = @SecurityAnswer,
                    Password = CASE WHEN @PasswordHash IS NOT NULL THEN @PasswordHash ELSE Password END
                WHERE Sid = @Id;
                """;
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        request.Id,
                        request.FirstName,
                        request.LastName,
                        request.RoleId,
                        request.CountryId,
                        SchoolId = primarySchoolId,
                        request.DateOfBirth,
                        request.Email,
                        request.StaffId,
                        request.SecurityQuestion,
                        request.SecurityAnswer,
                        PasswordHash = passwordHash
                    },
                    cancellationToken: cancellationToken));
            if (rows <= 0)
            {
                return AdminOperationResult.Fail("Staff was not updated.");
            }

            await _assignmentRepository.SaveSchoolIdsAsync(request.Id, schoolIds, cancellationToken);
            await _assignmentRepository.SaveRoleIdsAsync(request.Id, roleIds, request.DefaultRoleId, cancellationToken);
            return AdminOperationResult.Ok("Staff updated successfully.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
            return AdminOperationResult.Fail("Password is required for new staff.");

        const string existsSql = """
            SELECT TOP (1) CAST(Sid AS int) FROM LoginAccount
            WHERE LTRIM(RTRIM(CAST(LoginName AS varchar(50)))) = @LoginName;
            """;
        var existing = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(existsSql, new { request.LoginName }, cancellationToken: cancellationToken));
        if (existing.HasValue)
            return AdminOperationResult.Fail("A staff with this login name already exists.");

        const string insertSql = """
            INSERT INTO LoginAccount (LoginName, Password, FirstName, LastName, RoleID, CountryId, SchoolId, DOB, Email, Gender, SecurityQue, SecurityAns, Enabled, StaffId, AuthorizationLimit, HasSchoolAddAccess)
            VALUES (@LoginName, @PasswordHash, @FirstName, @LastName, @RoleId, @CountryId, @SchoolId, @DateOfBirth, @Email, '', @SecurityQuestion, @SecurityAnswer, 1, @StaffId, 0, 0);

            SELECT CAST(SCOPE_IDENTITY() AS INT);
            """;
        var newId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                insertSql,
                new
                {
                    request.LoginName,
                    PasswordHash = passwordHash,
                    request.FirstName,
                    request.LastName,
                    request.RoleId,
                    request.CountryId,
                    request.SchoolId,
                    request.DateOfBirth,
                    request.Email,
                    request.StaffId,
                    request.SecurityQuestion,
                    request.SecurityAnswer
                },
                cancellationToken: cancellationToken));
        if (newId <= 0)
        {
            return AdminOperationResult.Fail("Staff was not added.");
        }

        request.Id = newId;
        await _assignmentRepository.SaveSchoolIdsAsync(newId, schoolIds, cancellationToken);
        await _assignmentRepository.SaveRoleIdsAsync(newId, roleIds, request.DefaultRoleId, cancellationToken);
        return AdminOperationResult.Ok("Staff added successfully.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        try
        {
            await _assignmentRepository.DeleteAssignmentsAsync(id, cancellationToken);
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM LoginAccount WHERE Sid = @Id;",
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
