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

    private const string RoleNamesSql = """
        SELECT RoleID AS RoleId, LTRIM(RTRIM(ISNULL(RoleName, ''))) AS RoleName
        FROM RoleInfo
        WHERE RoleID IN @RoleIds;
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
    private readonly IStaffLoginAssignmentRepository _assignmentRepository;

    public AdminLoginRepository(
        IDbConnectionFactory connectionFactory,
        IAdminPermissionRepository permissionRepository,
        IStaffLoginAssignmentRepository assignmentRepository)
    {
        _connectionFactory = connectionFactory;
        _permissionRepository = permissionRepository;
        _assignmentRepository = assignmentRepository;
    }

    public Task<LoginAccountDto?> GetByLoginNameAsync(string loginName, CancellationToken cancellationToken = default)
        => GetByLoginNameInternalAsync(loginName, activeRoleId: null, cancellationToken);

    public Task<LoginAccountDto?> GetByLoginNameForRoleAsync(
        string loginName,
        int roleId,
        CancellationToken cancellationToken = default)
        => GetByLoginNameInternalAsync(loginName, activeRoleId: roleId, cancellationToken);

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

    private async Task<LoginAccountDto?> GetByLoginNameInternalAsync(
        string loginName,
        int? activeRoleId,
        CancellationToken cancellationToken)
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

        var assignedSchoolIds = await LoadAssignedSchoolIdsAsync(row, cancellationToken);
        var availableRoles = await LoadAvailableRolesAsync(dbConnection, row, cancellationToken);
        var resolvedActiveRoleId = ResolveActiveRoleId(activeRoleId, availableRoles, row.RoleId);
        var activeRole = availableRoles.FirstOrDefault(role => role.RoleId == resolvedActiveRoleId);

        IReadOnlyList<string> permissions = [];
        var isSuperAdmin = false;
        if (resolvedActiveRoleId > 0)
        {
            var permissionResult = await _permissionRepository.LoadForRoleAsync(resolvedActiveRoleId, cancellationToken);
            permissions = permissionResult.PermissionKeys;
            isSuperAdmin = permissionResult.IsSuperAdmin
                || string.Equals(activeRole?.RoleName, "Admin", StringComparison.OrdinalIgnoreCase);
        }

        var isSchoolScoped = !isSuperAdmin && assignedSchoolIds.Count > 0;
        var legacySchoolId = assignedSchoolIds.Count > 0 ? assignedSchoolIds[0] : row.SchoolId;

        return new LoginAccountDto
        {
            Id = row.Id,
            Username = row.Username,
            FirstName = row.FirstName,
            LastName = row.LastName,
            Email = row.Email,
            StoredPasswordHash = row.StoredPasswordHash ?? string.Empty,
            RoleId = resolvedActiveRoleId > 0 ? resolvedActiveRoleId : row.RoleId,
            RoleName = activeRole?.RoleName ?? row.RoleName,
            ActiveRoleId = resolvedActiveRoleId,
            SchoolId = legacySchoolId,
            IsActive = row.IsActive,
            IsSuperAdmin = isSuperAdmin,
            IsSchoolScoped = isSchoolScoped,
            AssignedSchoolIds = assignedSchoolIds,
            AvailableRoles = availableRoles,
            Permissions = permissions
        };
    }

    private async Task<IReadOnlyList<int>> LoadAssignedSchoolIdsAsync(
        LoginAccountRow row,
        CancellationToken cancellationToken)
    {
        var schoolIds = await _assignmentRepository.GetSchoolIdsAsync(row.Id, cancellationToken);
        if (schoolIds.Count > 0)
        {
            return schoolIds;
        }

        return row.SchoolId > 0 ? [row.SchoolId] : [];
    }

    private async Task<IReadOnlyList<LoginAccountRoleOptionDto>> LoadAvailableRolesAsync(
        DbConnection dbConnection,
        LoginAccountRow row,
        CancellationToken cancellationToken)
    {
        var roleIds = await _assignmentRepository.GetRoleIdsAsync(row.Id, cancellationToken);
        if (roleIds.Count == 0 && row.RoleId > 0)
        {
            roleIds = [row.RoleId];
        }

        if (roleIds.Count == 0)
        {
            return [];
        }

        var roleRows = await dbConnection.QueryAsync<(int RoleId, string RoleName)>(
            new CommandDefinition(
                RoleNamesSql,
                new { RoleIds = roleIds },
                cancellationToken: cancellationToken));

        var roleNames = roleRows.ToDictionary(role => role.RoleId, role => role.RoleName);
        var defaultRoleId = await _assignmentRepository.GetDefaultRoleIdAsync(row.Id, cancellationToken) ?? row.RoleId;

        return roleIds
            .Select(roleId => new LoginAccountRoleOptionDto
            {
                RoleId = roleId,
                RoleName = roleNames.TryGetValue(roleId, out var roleName) && !string.IsNullOrWhiteSpace(roleName)
                    ? roleName
                    : $"Role {roleId}",
                IsDefault = roleId == defaultRoleId
            })
            .OrderByDescending(role => role.IsDefault)
            .ThenBy(role => role.RoleName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static int ResolveActiveRoleId(
        int? requestedRoleId,
        IReadOnlyList<LoginAccountRoleOptionDto> availableRoles,
        int legacyRoleId)
    {
        if (requestedRoleId is > 0)
        {
            return availableRoles.Any(role => role.RoleId == requestedRoleId.Value)
                ? requestedRoleId.Value
                : 0;
        }

        if (availableRoles.Count == 1)
        {
            return availableRoles[0].RoleId;
        }

        if (availableRoles.Count > 1)
        {
            return 0;
        }

        return legacyRoleId;
    }

    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>
        string.Equals(stored.Trim(), md5Hash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stored.Trim(), plainPassword, StringComparison.Ordinal);

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
