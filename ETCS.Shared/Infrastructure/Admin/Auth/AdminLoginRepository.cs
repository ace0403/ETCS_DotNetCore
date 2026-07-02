using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Security;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Auth;

public sealed class AdminLoginRepository : IAdminLoginRepository
{
    private const string LoginAccountSql = """
        SELECT TOP (1)
            CAST(la.Sid AS int) AS Id,
            LTRIM(RTRIM(CAST(la.LoginName AS varchar(50)))) AS Username,
            LTRIM(RTRIM(ISNULL(la.FirstName, ''))) AS FirstName,
            LTRIM(RTRIM(ISNULL(la.LastName, ''))) AS LastName,
            LTRIM(RTRIM(CAST(la.Email AS varchar(100)))) AS Email,
            la.Password AS StoredPasswordHash,
            la.RoleID AS RoleId,
            LTRIM(RTRIM(ISNULL(ri.RoleName, 'Admin'))) AS RoleName,
            CAST(ISNULL(la.SchoolId, 0) AS int) AS SchoolId,
            CAST(ISNULL(la.Enabled, 0) AS bit) AS IsActive
        FROM LoginAccount la
        LEFT JOIN RoleInfo ri ON ri.RoleID = la.RoleID
        WHERE (
            LTRIM(RTRIM(CAST(la.LoginName AS varchar(50)))) = @LoginName
            OR LTRIM(RTRIM(CAST(la.Email AS varchar(100)))) = @LoginName
        )
          AND ISNULL(la.Enabled, 0) = 1;
        """;

    private const string UpdatePasswordSql = """
        UPDATE LoginAccount
        SET Password = @PasswordHash
        WHERE Sid = @Id;
        """;

    private const string GetPasswordSql = """
        SELECT TOP (1) Password FROM LoginAccount WHERE Sid = @Id;
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IAdminPermissionRepository _permissionRepository;

    public AdminLoginRepository(
        IDbConnectionFactory connectionFactory,
        IAdminPermissionRepository permissionRepository)
    {
        _connectionFactory = connectionFactory;
        _permissionRepository = permissionRepository;
    }

    public async Task<LoginAccountDto?> GetByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
    {
        var normalized = loginName.Trim();
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QuerySingleOrDefaultAsync<LoginAccountRow>(
            new CommandDefinition(
                LoginAccountSql,
                new { LoginName = normalized },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var permissionResult = await _permissionRepository.LoadForRoleAsync(row.RoleId, cancellationToken);
        var isSuperAdmin = permissionResult.IsSuperAdmin
            || string.Equals(row.RoleName, "Admin", StringComparison.OrdinalIgnoreCase);

        var assignedSchoolIds = BuildAssignedSchoolIds(row.SchoolId);
        var isSchoolScoped = !isSuperAdmin && assignedSchoolIds.Count > 0;

        return new LoginAccountDto
        {
            Id = row.Id,
            Username = row.Username,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Email = row.Email,
            StoredPasswordHash = row.StoredPasswordHash ?? string.Empty,
            RoleId = row.RoleId,
            RoleName = row.RoleName,
            SchoolId = row.SchoolId,
            IsActive = row.IsActive,
            IsSuperAdmin = isSuperAdmin,
            IsSchoolScoped = isSchoolScoped,
            AssignedSchoolIds = assignedSchoolIds,
            Permissions = permissionResult.PermissionKeys
        };
    }

    public async Task<AdminOperationResult> ChangePasswordAsync(
        int accountId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var stored = await dbConnection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                GetPasswordSql,
                new { Id = accountId },
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(stored))
        {
            return AdminOperationResult.Fail("Account not found.");
        }

        var currentHash = SecurityHelper.GetMd5Hash(currentPassword);
        if (!PasswordMatches(stored, currentHash, currentPassword))
        {
            return AdminOperationResult.Fail("Current password is incorrect.");
        }

        var newHash = SecurityHelper.GetMd5Hash(newPassword);
        if (PasswordMatches(stored, newHash, newPassword))
        {
            return AdminOperationResult.Fail("New password must be different from the current password.");
        }

        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePasswordSql,
                new { Id = accountId, PasswordHash = newHash },
                cancellationToken: cancellationToken));

        return rows > 0
            ? AdminOperationResult.Ok("Password updated successfully.")
            : AdminOperationResult.Fail("Password was not updated.");
    }

    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>
        string.Equals(stored.Trim(), md5Hash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stored.Trim(), plainPassword, StringComparison.Ordinal);

    private static IReadOnlyList<int> BuildAssignedSchoolIds(int schoolId) =>
        schoolId > 0 ? [schoolId] : [];

    private sealed class LoginAccountRow
    {
        public int Id { get; init; }
        public string Username { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public string Email { get; init; } = string.Empty;
        public string? StoredPasswordHash { get; init; }
        public int RoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public bool IsActive { get; init; }
    }
}
