using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemSchoolAdminRepository : IMealItemSchoolAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealItemSchoolAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetSchoolIdsAsync(
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
                SELECT SchoolId
                FROM MealItemSchools
                WHERE MealItemId = @MealItemId
                ORDER BY SchoolId;
                """,
                new { MealItemId = mealItemId },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task SaveSchoolIdsAsync(
        int mealItemId,
        IReadOnlyList<int> schoolIds,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await SaveSchoolIdsInternalAsync(dbConnection, null, mealItemId, schoolIds, cancellationToken);
    }

    public Task SaveSchoolIdsAsync(
        int mealItemId,
        IReadOnlyList<int> schoolIds,
        DbConnection connection,
        DbTransaction transaction,
        CancellationToken cancellationToken = default) =>
        SaveSchoolIdsInternalAsync(connection, transaction, mealItemId, schoolIds, cancellationToken);

    public async Task DeleteSchoolIdsAsync(int mealItemId, CancellationToken cancellationToken = default)
    {
        await SaveSchoolIdsAsync(mealItemId, [], cancellationToken);
    }

    private static async Task SaveSchoolIdsInternalAsync(
        DbConnection connection,
        DbTransaction? transaction,
        int mealItemId,
        IReadOnlyList<int> schoolIds,
        CancellationToken cancellationToken)
    {
        if (mealItemId <= 0) return;

        if (transaction is null)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM MealItemSchools WHERE MealItemId = @MealItemId;",
                new { MealItemId = mealItemId },
                transaction: transaction,
                cancellationToken: cancellationToken));

        if (schoolIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO MealItemSchools (MealItemId, SchoolId, CreatedOn)
            VALUES (@MealItemId, @SchoolId, GETDATE());
            """;

        foreach (var schoolId in schoolIds.Distinct().Where(id => id > 0))
        {
            await connection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { MealItemId = mealItemId, SchoolId = schoolId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
        }
    }
}
