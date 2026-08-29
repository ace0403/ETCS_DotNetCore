using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public sealed class StudentOrderTypeAdminRepository : IStudentOrderTypeAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public StudentOrderTypeAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(
        decimal studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0) return [];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                """
                SELECT OrderTypeId
                FROM StudentOrderTypes
                WHERE StudentId = @StudentId;
                """,
                new { StudentId = studentId },
                cancellationToken: cancellationToken));

        return rows
            .Where(id => StudentOrderTypeOptionIds.Selectable.Contains(id))
            .ToList();
    }

    public async Task SaveOrderTypesAsync(
        decimal studentId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM StudentOrderTypes WHERE StudentId = @StudentId;",
                new { StudentId = studentId },
                cancellationToken: cancellationToken));

        if (orderTypeIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO StudentOrderTypes (StudentId, OrderTypeId, CreatedOn)
            VALUES (@StudentId, @OrderTypeId, GETDATE());
            """;

        foreach (var orderTypeId in orderTypeIds.Distinct())
        {
            if (!StudentOrderTypeOptionIds.Selectable.Contains(orderTypeId)) continue;
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { StudentId = studentId, OrderTypeId = orderTypeId },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task DeleteOrderTypesAsync(decimal studentId, CancellationToken cancellationToken = default)
    {
        await SaveOrderTypesAsync(studentId, [], cancellationToken);
    }
}
