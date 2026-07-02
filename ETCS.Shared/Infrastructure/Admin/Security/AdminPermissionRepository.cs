using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Security;

public sealed class AdminPermissionRepository : IAdminPermissionRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public AdminPermissionRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AdminRolePermissionLoadResult> LoadForRoleAsync(
        int roleId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await LoadForRoleInternalAsync(roleId, cancellationToken);
        }
        catch
        {
            return new AdminRolePermissionLoadResult();
        }
    }

    private async Task<AdminRolePermissionLoadResult> LoadForRoleInternalAsync(
        int roleId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var role = await dbConnection.QuerySingleOrDefaultAsync<RoleRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    RoleId,
                    LTRIM(RTRIM(RoleName)) AS RoleName,
                    CAST(ISNULL(IsSuperAdmin, 0) AS bit) AS IsSuperAdmin
                FROM AdminRole
                WHERE RoleId = @RoleId AND IsActive = 1;
                """,
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        if (role is null)
        {
            return new AdminRolePermissionLoadResult();
        }

        if (role.IsSuperAdmin)
        {
            var modules = await ListModulesInternalAsync(dbConnection, cancellationToken);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var module in modules)
            {
                keys.Add($"{module.ModuleKey}.View");
                keys.Add($"{module.ModuleKey}.Add");
                keys.Add($"{module.ModuleKey}.Edit");
                keys.Add($"{module.ModuleKey}.Delete");
            }

            return new AdminRolePermissionLoadResult
            {
                IsSuperAdmin = true,
                PermissionKeys = keys.ToList()
            };
        }

        var rows = await dbConnection.QueryAsync<PermissionRow>(
            new CommandDefinition(
                """
                SELECT
                    m.ModuleKey,
                    CAST(ISNULL(p.CanView, 0) AS bit) AS CanView,
                    CAST(ISNULL(p.CanAdd, 0) AS bit) AS CanAdd,
                    CAST(ISNULL(p.CanEdit, 0) AS bit) AS CanEdit,
                    CAST(ISNULL(p.CanDelete, 0) AS bit) AS CanDelete
                FROM AdminRolePermission p
                INNER JOIN AdminModule m ON m.ModuleId = p.ModuleId
                WHERE p.RoleId = @RoleId
                  AND m.IsActive = 1;
                """,
                new { RoleId = roleId },
                cancellationToken: cancellationToken));

        var permissionKeys = new List<string>();
        foreach (var row in rows)
        {
            if (row.CanView) permissionKeys.Add($"{row.ModuleKey}.View");
            if (row.CanAdd) permissionKeys.Add($"{row.ModuleKey}.Add");
            if (row.CanEdit) permissionKeys.Add($"{row.ModuleKey}.Edit");
            if (row.CanDelete) permissionKeys.Add($"{row.ModuleKey}.Delete");
        }

        return new AdminRolePermissionLoadResult
        {
            IsSuperAdmin = false,
            PermissionKeys = permissionKeys
        };
    }

    public async Task<IReadOnlyList<AdminModuleDto>> ListModulesAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await ListModulesInternalAsync(dbConnection, cancellationToken);
    }

    private static async Task<IReadOnlyList<AdminModuleDto>> ListModulesInternalAsync(
        DbConnection dbConnection,
        CancellationToken cancellationToken)
    {
        var rows = await dbConnection.QueryAsync<AdminModuleDto>(
            new CommandDefinition(
                """
                SELECT
                    ModuleId,
                    LTRIM(RTRIM(ModuleKey)) AS ModuleKey,
                    LTRIM(RTRIM(DisplayName)) AS DisplayName,
                    LTRIM(RTRIM(GroupName)) AS GroupName,
                    LTRIM(RTRIM(ControllerName)) AS ControllerName,
                    LTRIM(RTRIM(ActionName)) AS ActionName,
                    ISNULL(SortOrder, 0) AS SortOrder,
                    CAST(ISNULL(IsActive, 1) AS bit) AS IsActive
                FROM AdminModule
                WHERE IsActive = 1
                ORDER BY SortOrder, DisplayName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    private sealed class RoleRow
    {
        public int RoleId { get; init; }
        public string RoleName { get; init; } = string.Empty;
        public bool IsSuperAdmin { get; init; }
    }

    private sealed class PermissionRow
    {
        public string ModuleKey { get; init; } = string.Empty;
        public bool CanView { get; init; }
        public bool CanAdd { get; init; }
        public bool CanEdit { get; init; }
        public bool CanDelete { get; init; }
    }
}
