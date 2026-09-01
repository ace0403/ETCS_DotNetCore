using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Auth;

public sealed class StaffLoginAssignmentRepository : IStaffLoginAssignmentRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public StaffLoginAssignmentRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetSchoolIdsAsync(
        int loginAccountId,
        CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT SchoolId
                FROM LoginAccountSchool
                WHERE LoginAccountId = @LoginAccountId
                ORDER BY SchoolId;
                """,
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));

        return rows.Where(id => id > 0).Distinct().ToList();
    }

    public async Task<IReadOnlyList<int>> GetRoleIdsAsync(
        int loginAccountId,
        CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT RoleId
                FROM LoginAccountRole
                WHERE LoginAccountId = @LoginAccountId
                ORDER BY RoleId;
                """,
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));

        return rows.Where(id => id > 0).Distinct().ToList();
    }

    public async Task<int?> GetDefaultRoleIdAsync(
        int loginAccountId,
        CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return null;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var defaultRoleId = await dbConnection.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(
                """
                SELECT TOP (1) RoleId
                FROM LoginAccountRole
                WHERE LoginAccountId = @LoginAccountId
                ORDER BY IsDefault DESC, RoleId;
                """,
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));

        return defaultRoleId is > 0 ? defaultRoleId : null;
    }

    public async Task<IReadOnlyList<int>> GetLoginAccountIdsBySchoolAsync(
        int schoolId,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT LoginAccountId
                FROM LoginAccountSchool
                WHERE SchoolId = @SchoolId;
                """,
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken));

        return rows.Where(id => id > 0).Distinct().ToList();
    }

    public async Task SaveSchoolIdsAsync(
        int loginAccountId,
        IReadOnlyList<int> schoolIds,
        CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM LoginAccountSchool WHERE LoginAccountId = @LoginAccountId;",
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO LoginAccountSchool (LoginAccountId, SchoolId)
            VALUES (@LoginAccountId, @SchoolId);
            """;

        foreach (var schoolId in schoolIds.Where(id => id > 0).Distinct())
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { LoginAccountId = loginAccountId, SchoolId = schoolId },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task SaveRoleIdsAsync(
        int loginAccountId,
        IReadOnlyList<int> roleIds,
        int? defaultRoleId,
        CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return;

        var distinctRoleIds = roleIds.Where(id => id > 0).Distinct().ToList();
        var resolvedDefaultRoleId = defaultRoleId is > 0 && distinctRoleIds.Contains(defaultRoleId.Value)
            ? defaultRoleId.Value
            : distinctRoleIds.FirstOrDefault();

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM LoginAccountRole WHERE LoginAccountId = @LoginAccountId;",
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));

        const string insertSql = """
            INSERT INTO LoginAccountRole (LoginAccountId, RoleId, IsDefault)
            VALUES (@LoginAccountId, @RoleId, @IsDefault);
            """;

        foreach (var roleId in distinctRoleIds)
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new
                    {
                        LoginAccountId = loginAccountId,
                        RoleId = roleId,
                        IsDefault = roleId == resolvedDefaultRoleId
                    },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task DeleteAssignmentsAsync(int loginAccountId, CancellationToken cancellationToken = default)
    {
        if (loginAccountId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                DELETE FROM LoginAccountSchool WHERE LoginAccountId = @LoginAccountId;
                DELETE FROM LoginAccountRole WHERE LoginAccountId = @LoginAccountId;
                """,
                new { LoginAccountId = loginAccountId },
                cancellationToken: cancellationToken));
    }
}
