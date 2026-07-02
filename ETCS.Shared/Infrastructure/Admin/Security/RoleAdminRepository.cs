using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Security;

public sealed class RoleAdminRepository : IRoleAdminRepository
{
    private const string SelectSql = """
        SELECT
            r.RoleId,
            LTRIM(RTRIM(r.RoleName)) AS RoleName,
            CAST(ISNULL(r.IsSuperAdmin, 0) AS bit) AS IsSuperAdmin,
            CAST(ISNULL(r.IsSystem, 0) AS bit) AS IsSystem,
            CAST(ISNULL(r.IsActive, 1) AS bit) AS IsActive,
            CAST(0 AS int) AS UserCount
        """;

    private const string FromSql = "FROM AdminRole r";

    private const string SearchFilterSql = "LTRIM(RTRIM(r.RoleName)) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RoleId"] = "r.RoleId",
        ["RoleName"] = "r.RoleName",
        ["IsSuperAdmin"] = "r.IsSuperAdmin",
        ["IsSystem"] = "r.IsSystem",
        ["IsActive"] = "r.IsActive",
        ["UserCount"] = "r.RoleId"
    };

    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly IDbConnectionFactory _connectionFactory;

    public RoleAdminRepository(
        IMealDbConnectionFactory mealDbConnectionFactory,
        IDbConnectionFactory connectionFactory)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _connectionFactory = connectionFactory;
    }

    public async Task<DataTableResponse<AdminRoleListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var response = await QueryPagedAsync<AdminRoleListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            null,
            SearchFilterSql,
            SortColumns,
            "r.RoleName",
            request,
            cancellationToken: cancellationToken);

        if (response.Data.Count == 0)
        {
            return response;
        }

        using var mainConnection = _connectionFactory.CreateConnection();
        var mainDb = (DbConnection)mainConnection;
        await mainDb.OpenAsync(cancellationToken);

        var counts = (await mainDb.QueryAsync<(int RoleId, int UserCount)>(
            new CommandDefinition(
                """
                SELECT RoleID AS RoleId, COUNT(1) AS UserCount
                FROM LoginAccount
                GROUP BY RoleID;
                """,
                cancellationToken: cancellationToken))).ToDictionary(x => x.RoleId, x => x.UserCount);

        response.Data = response.Data
            .Select(row => new AdminRoleListItemDto
            {
                RoleId = row.RoleId,
                RoleName = row.RoleName,
                IsSuperAdmin = row.IsSuperAdmin,
                IsSystem = row.IsSystem,
                IsActive = row.IsActive,
                UserCount = counts.TryGetValue(row.RoleId, out var count) ? count : 0
            })
            .ToList();

        return response;
    }

    public async Task<AdminRoleDetailDto> GetTemplateAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var modules = await dbConnection.QueryAsync<AdminRolePermissionRowDto>(
            new CommandDefinition(
                """
                SELECT
                    ModuleId,
                    LTRIM(RTRIM(ModuleKey)) AS ModuleKey,
                    LTRIM(RTRIM(DisplayName)) AS DisplayName,
                    LTRIM(RTRIM(GroupName)) AS GroupName,
                    CAST(0 AS bit) AS CanView,
                    CAST(0 AS bit) AS CanAdd,
                    CAST(0 AS bit) AS CanEdit,
                    CAST(0 AS bit) AS CanDelete
                FROM AdminModule
                WHERE IsActive = 1
                ORDER BY SortOrder, DisplayName;
                """,
                cancellationToken: cancellationToken));

        return new AdminRoleDetailDto
        {
            Permissions = modules.ToList()
        };
    }

    public async Task<AdminRoleDetailDto?> GetAsync(int roleId, CancellationToken cancellationToken = default)
    {
        if (roleId <= 0) return null;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var role = await dbConnection.QuerySingleOrDefaultAsync<AdminRoleDetailDto>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    RoleId,
                    LTRIM(RTRIM(RoleName)) AS RoleName,
                    CAST(ISNULL(IsSuperAdmin, 0) AS bit) AS IsSuperAdmin,
                    CAST(ISNULL(IsSystem, 0) AS bit) AS IsSystem,
                    CAST(ISNULL(IsActive, 1) AS bit) AS IsActive
                FROM AdminRole
                WHERE RoleId = @RoleId;
                """,
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        if (role is null) return null;

        var modules = await dbConnection.QueryAsync<AdminRolePermissionRowDto>(
            new CommandDefinition(
                """
                SELECT
                    m.ModuleId,
                    LTRIM(RTRIM(m.ModuleKey)) AS ModuleKey,
                    LTRIM(RTRIM(m.DisplayName)) AS DisplayName,
                    LTRIM(RTRIM(m.GroupName)) AS GroupName,
                    CAST(ISNULL(p.CanView, 0) AS bit) AS CanView,
                    CAST(ISNULL(p.CanAdd, 0) AS bit) AS CanAdd,
                    CAST(ISNULL(p.CanEdit, 0) AS bit) AS CanEdit,
                    CAST(ISNULL(p.CanDelete, 0) AS bit) AS CanDelete
                FROM AdminModule m
                LEFT JOIN AdminRolePermission p
                    ON p.ModuleId = m.ModuleId AND p.RoleId = @RoleId
                WHERE m.IsActive = 1
                ORDER BY m.SortOrder, m.DisplayName;
                """,
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        var permissionList = modules.ToList();
        if (role.IsSuperAdmin)
        {
            permissionList = permissionList
                .Select(m => new AdminRolePermissionRowDto
                {
                    ModuleId = m.ModuleId,
                    ModuleKey = m.ModuleKey,
                    DisplayName = m.DisplayName,
                    GroupName = m.GroupName,
                    CanView = true,
                    CanAdd = true,
                    CanEdit = true,
                    CanDelete = true
                })
                .ToList();
        }

        return new AdminRoleDetailDto
        {
            RoleId = role.RoleId,
            RoleName = role.RoleName,
            IsSuperAdmin = role.IsSuperAdmin,
            IsSystem = role.IsSystem,
            IsActive = role.IsActive,
            Permissions = permissionList
        };
    }

    public async Task<AdminOperationResult> SaveAsync(
        AdminRoleSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RoleName))
        {
            return AdminOperationResult.Fail("Role name is required.");
        }

        var roleName = request.RoleName.Trim();

        using var mealConnection = _mealDbConnectionFactory.CreateConnection();
        var mealDb = (DbConnection)mealConnection;
        await mealDb.OpenAsync(cancellationToken);

        if (request.RoleId > 0)
        {
            var existing = await mealDb.QuerySingleOrDefaultAsync<RoleMetaRow>(
                new CommandDefinition(
                    """
                    SELECT TOP (1)
                        RoleId,
                        LTRIM(RTRIM(RoleName)) AS RoleName,
                        CAST(ISNULL(IsSuperAdmin, 0) AS bit) AS IsSuperAdmin,
                        CAST(ISNULL(IsSystem, 0) AS bit) AS IsSystem
                    FROM AdminRole
                    WHERE RoleId = @RoleId;
                    """,
                    new { request.RoleId },
                    cancellationToken: cancellationToken));

            if (existing is null)
            {
                return AdminOperationResult.Fail("Role not found.");
            }

            if (existing.IsSuperAdmin)
            {
                await mealDb.ExecuteAsync(
                    new CommandDefinition(
                        """
                        UPDATE AdminRole
                        SET IsActive = @IsActive
                        WHERE RoleId = @RoleId;
                        """,
                        new { request.RoleId, request.IsActive },
                        cancellationToken: cancellationToken));

                return AdminOperationResult.Ok("Admin role updated.");
            }

            var duplicate = await mealDb.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    """
                    SELECT COUNT(1)
                    FROM AdminRole
                    WHERE RoleName = @RoleName AND RoleId <> @RoleId;
                    """,
                    new { RoleName = roleName, request.RoleId },
                    cancellationToken: cancellationToken));

            if (duplicate > 0)
            {
                return AdminOperationResult.Fail("A role with this name already exists.");
            }

            await mealDb.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE AdminRole
                    SET RoleName = @RoleName,
                        IsActive = @IsActive
                    WHERE RoleId = @RoleId;
                    """,
                    new { request.RoleId, RoleName = roleName, request.IsActive },
                    cancellationToken: cancellationToken));

            await SyncRoleInfoAsync(roleName, request.RoleId, cancellationToken);
            await SavePermissionsAsync(mealDb, request.RoleId, request.Permissions, cancellationToken);

            return AdminOperationResult.Ok("Role updated successfully.");
        }

        var newRoleId = await AllocateRoleIdAsync(cancellationToken);
        if (newRoleId <= 0)
        {
            return AdminOperationResult.Fail("Could not allocate a new role id.");
        }

        var nameExists = await mealDb.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM AdminRole WHERE RoleName = @RoleName;",
                new { RoleName = roleName },
                cancellationToken: cancellationToken));

        if (nameExists > 0)
        {
            return AdminOperationResult.Fail("A role with this name already exists.");
        }

        await mealDb.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO AdminRole (RoleId, RoleName, IsSuperAdmin, IsSystem, IsActive)
                VALUES (@RoleId, @RoleName, 0, 0, @IsActive);
                """,
                new { RoleId = newRoleId, RoleName = roleName, request.IsActive },
                cancellationToken: cancellationToken));

        await SyncRoleInfoAsync(roleName, newRoleId, cancellationToken);
        await SavePermissionsAsync(mealDb, newRoleId, request.Permissions, cancellationToken);

        return AdminOperationResult.Ok("Role added successfully.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int roleId, CancellationToken cancellationToken = default)
    {
        if (roleId <= 0) return AdminOperationResult.Fail("Role id is required.");

        using var mealConnection = _mealDbConnectionFactory.CreateConnection();
        var mealDb = (DbConnection)mealConnection;
        await mealDb.OpenAsync(cancellationToken);

        var role = await mealDb.QuerySingleOrDefaultAsync<RoleMetaRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    RoleId,
                    LTRIM(RTRIM(RoleName)) AS RoleName,
                    CAST(ISNULL(IsSuperAdmin, 0) AS bit) AS IsSuperAdmin,
                    CAST(ISNULL(IsSystem, 0) AS bit) AS IsSystem
                FROM AdminRole
                WHERE RoleId = @RoleId;
                """,
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        if (role is null) return AdminOperationResult.Fail("Role not found.");
        if (role.IsSystem) return AdminOperationResult.Fail("System roles cannot be deleted.");

        using var mainConnection = _connectionFactory.CreateConnection();
        var mainDb = (DbConnection)mainConnection;
        await mainDb.OpenAsync(cancellationToken);

        var userCount = await mainDb.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM LoginAccount WHERE RoleID = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        if (userCount > 0)
        {
            return AdminOperationResult.Fail("Role is assigned to staff and cannot be deleted.");
        }

        await mealDb.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM AdminRolePermission WHERE RoleId = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        await mealDb.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM AdminRole WHERE RoleId = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        await mainDb.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM RoleInfo WHERE RoleID = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        return AdminOperationResult.Ok("Role deleted successfully.");
    }

    public async Task<IReadOnlyList<AdminRoleLookupDto>> RoleLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<AdminRoleLookupDto>(
            new CommandDefinition(
                """
                SELECT
                    RoleId AS Id,
                    LTRIM(RTRIM(RoleName)) AS Name
                FROM AdminRole
                WHERE IsActive = 1
                ORDER BY RoleName;
                """,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private async Task<int> AllocateRoleIdAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT ISNULL(MAX(RoleID), 0) + 1 FROM RoleInfo;",
                cancellationToken: cancellationToken));
    }

    private async Task SyncRoleInfoAsync(string roleName, int roleId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var exists = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                "SELECT COUNT(1) FROM RoleInfo WHERE RoleID = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        if (exists > 0)
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "UPDATE RoleInfo SET RoleName = @RoleName WHERE RoleID = @RoleId;",
                    new { RoleId = roleId, RoleName = roleName },
                    cancellationToken: cancellationToken));
            return;
        }

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO RoleInfo (RoleID, RoleName) VALUES (@RoleId, @RoleName);",
                new { RoleId = roleId, RoleName = roleName },
                cancellationToken: cancellationToken));
    }

    private static async Task SavePermissionsAsync(
        DbConnection mealDb,
        int roleId,
        IReadOnlyList<AdminRolePermissionSaveItem> permissions,
        CancellationToken cancellationToken)
    {
        await mealDb.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM AdminRolePermission WHERE RoleId = @RoleId;",
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        foreach (var item in permissions)
        {
            if (!item.CanView && !item.CanAdd && !item.CanEdit && !item.CanDelete)
            {
                continue;
            }

            await mealDb.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO AdminRolePermission (RoleId, ModuleId, CanView, CanAdd, CanEdit, CanDelete)
                    VALUES (@RoleId, @ModuleId, @CanView, @CanAdd, @CanEdit, @CanDelete);
                    """,
                    new
                    {
                        RoleId = roleId,
                        item.ModuleId,
                        item.CanView,
                        item.CanAdd,
                        item.CanEdit,
                        item.CanDelete
                    },
                    cancellationToken: cancellationToken));
        }
    }

    private sealed class RoleMetaRow
    {
        public int RoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public bool IsSuperAdmin { get; init; }
        public bool IsSystem { get; init; }
    }
}
