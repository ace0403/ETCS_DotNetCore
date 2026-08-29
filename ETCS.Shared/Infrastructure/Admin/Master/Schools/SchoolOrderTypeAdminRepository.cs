using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public sealed class SchoolOrderTypeAdminRepository : ISchoolOrderTypeAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public SchoolOrderTypeAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(
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
                SELECT OrderTypeId
                FROM SchoolOrderTypes
                WHERE SchoolId = @SchoolId;
                """,
                new { SchoolId = schoolId },
                cancellationToken: cancellationToken));

        return rows
            .Where(id => StudentOrderTypeOptionIds.Selectable.Contains(id))
            .ToList();
    }

    public async Task SaveOrderTypesAsync(
        int schoolId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await SaveOrderTypesCoreAsync(dbConnection, null, schoolId, orderTypeIds, cancellationToken);
    }

    public async Task DeleteOrderTypesAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        await SaveOrderTypesAsync(schoolId, [], cancellationToken);
    }

    internal static async Task SaveOrderTypesCoreAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int schoolId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken)
    {
        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM SchoolOrderTypes WHERE SchoolId = @SchoolId;",
                new { SchoolId = schoolId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (orderTypeIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO SchoolOrderTypes (SchoolId, OrderTypeId, CreatedOn)
            VALUES (@SchoolId, @OrderTypeId, GETDATE());
            """;

        foreach (var orderTypeId in orderTypeIds.Distinct())
        {
            if (!StudentOrderTypeOptionIds.Selectable.Contains(orderTypeId)) continue;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { SchoolId = schoolId, OrderTypeId = orderTypeId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
        }
    }
}
