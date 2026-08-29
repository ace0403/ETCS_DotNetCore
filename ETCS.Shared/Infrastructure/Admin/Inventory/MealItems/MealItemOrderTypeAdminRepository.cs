using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemOrderTypeAdminRepository : IMealItemOrderTypeAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealItemOrderTypeAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(
        int mealItemId,
        CancellationToken cancellationToken = default)
    {
        if (mealItemId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT OrderTypeId
                FROM MealItemOrderTypes
                WHERE MealItemId = @MealItemId;
                """,
                new { MealItemId = mealItemId },
                cancellationToken: cancellationToken));

        return rows
            .Where(id => MealItemChannelOptionIds.Selectable.Contains(id))
            .ToList();
    }

    public async Task SaveOrderTypesAsync(
        int mealItemId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await SaveOrderTypesInternalAsync(dbConnection, null, mealItemId, orderTypeIds, cancellationToken);
    }

    public Task SaveOrderTypesAsync(
        int mealItemId,
        IReadOnlyList<int> orderTypeIds,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        SaveOrderTypesInternalAsync(connection, transaction, mealItemId, orderTypeIds, cancellationToken);

    public async Task DeleteOrderTypesAsync(int mealItemId, CancellationToken cancellationToken = default)
    {
        await SaveOrderTypesAsync(mealItemId, [], cancellationToken);
    }

    private static async Task SaveOrderTypesInternalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int mealItemId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken)
    {
        if (mealItemId <= 0) return;

        if (transaction is null)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM MealItemOrderTypes WHERE MealItemId = @MealItemId;",
                new { MealItemId = mealItemId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (orderTypeIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO MealItemOrderTypes (MealItemId, OrderTypeId, CreatedOn)
            VALUES (@MealItemId, @OrderTypeId, GETDATE());
            """;

        foreach (var orderTypeId in orderTypeIds.Distinct())
        {
            if (!MealItemChannelOptionIds.Selectable.Contains(orderTypeId)) continue;
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { MealItemId = mealItemId, OrderTypeId = orderTypeId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
        }
    }
}
