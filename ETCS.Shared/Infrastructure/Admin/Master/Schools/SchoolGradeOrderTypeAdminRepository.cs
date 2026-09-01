using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public sealed class SchoolGradeOrderTypeAdminRepository : ISchoolGradeOrderTypeAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public SchoolGradeOrderTypeAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SchoolGradeOrderTypeConfigDto>> GetConfigsAsync(
        int schoolId,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var settings = await dbConnection.QueryAsync<(int GradeId, bool IsNoService)>(
            new CommandDefinition(
                """
                SELECT GradeId, CAST(ISNULL(IsNoService, 0) AS bit) AS IsNoService
                FROM SchoolGradeOrderTypeSettings
                WHERE SchoolId = @SchoolId;
                """,
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken));

        var orderTypes = await dbConnection.QueryAsync<(int GradeId, int OrderTypeId)>(
            new CommandDefinition(
                """
                SELECT GradeId, OrderTypeId
                FROM SchoolGradeOrderTypes
                WHERE SchoolId = @SchoolId;
                """,
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken));

        var orderTypesByGrade = orderTypes
            .Where(row => StudentOrderTypeOptionIds.Selectable.Contains(row.OrderTypeId))
            .GroupBy(row => row.GradeId)
            .ToDictionary(group => group.Key, group => group.Select(row => row.OrderTypeId).ToList());

        return settings
            .Select(setting => new SchoolGradeOrderTypeConfigDto
            {
                GradeId = setting.GradeId,
                IsNoService = setting.IsNoService,
                OrderTypeIds = orderTypesByGrade.TryGetValue(setting.GradeId, out var ids) ? ids : []
            })
            .ToList();
    }

    public async Task<SchoolGradeOrderTypeAccessDto> GetAccessAsync(
        int schoolId,
        int gradeId,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0 || gradeId <= 0)
        {
            return new SchoolGradeOrderTypeAccessDto { IsConfigured = false };
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var setting = await dbConnection.QuerySingleOrDefaultAsync<GradeSettingRow>(
            new CommandDefinition(
                """
                SELECT CAST(ISNULL(IsNoService, 0) AS bit) AS IsNoService
                FROM SchoolGradeOrderTypeSettings
                WHERE SchoolId = @SchoolId AND GradeId = @GradeId;
                """,
                new { SchoolId = schoolId, GradeId = gradeId },
                cancellationToken: cancellationToken));

        if (setting is null)
        {
            return new SchoolGradeOrderTypeAccessDto { IsConfigured = false };
        }

        if (setting.IsNoService)
        {
            return new SchoolGradeOrderTypeAccessDto
            {
                IsConfigured = true,
                IsNoService = true,
                OrderTypeIds = []
            };
        }

        var orderTypeIds = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT OrderTypeId
                FROM SchoolGradeOrderTypes
                WHERE SchoolId = @SchoolId AND GradeId = @GradeId;
                """,
                new { SchoolId = schoolId, GradeId = gradeId },
                cancellationToken: cancellationToken));

        return new SchoolGradeOrderTypeAccessDto
        {
            IsConfigured = true,
            IsNoService = false,
            OrderTypeIds = orderTypeIds
                .Where(id => StudentOrderTypeOptionIds.Selectable.Contains(id))
                .ToList()
        };
    }

    public async Task SaveConfigsAsync(
        int schoolId,
        IReadOnlyList<SchoolGradeOrderTypeConfigDto> configs,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM SchoolGradeOrderTypes WHERE SchoolId = @SchoolId;",
                new { SchoolId = schoolId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM SchoolGradeOrderTypeSettings WHERE SchoolId = @SchoolId;",
                new { SchoolId = schoolId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        const string insertSettingSql = """
            INSERT INTO SchoolGradeOrderTypeSettings (SchoolId, GradeId, IsNoService, CreatedOn)
            VALUES (@SchoolId, @GradeId, @IsNoService, GETDATE());
            """;

        const string insertOrderTypeSql = """
            INSERT INTO SchoolGradeOrderTypes (SchoolId, GradeId, OrderTypeId, CreatedOn)
            VALUES (@SchoolId, @GradeId, @OrderTypeId, GETDATE());
            """;

        foreach (var config in configs.Where(c => c.GradeId > 0).GroupBy(c => c.GradeId).Select(g => g.Last()))
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    insertSettingSql,
                    new { SchoolId = schoolId, config.GradeId, IsNoService = config.IsNoService },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            if (config.IsNoService) continue;

            foreach (var orderTypeId in (config.OrderTypeIds ?? []).Distinct())
            {
                if (!StudentOrderTypeOptionIds.Selectable.Contains(orderTypeId)) continue;
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        insertOrderTypeSql,
                        new { SchoolId = schoolId, config.GradeId, OrderTypeId = orderTypeId },
                        transaction: transaction,
                        cancellationToken: cancellationToken));
            }
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task DeleteConfigsAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        await SaveConfigsAsync(schoolId, [], cancellationToken);
    }

    private sealed class GradeSettingRow
    {
        public bool IsNoService { get; init; }
    }
}
