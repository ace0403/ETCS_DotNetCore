using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public sealed class StudentAllergyAdminRepository : IStudentAllergyAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public StudentAllergyAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<int>> GetAllergyIdsAsync(
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
                SELECT AllergyItemId
                FROM StudentAllergies
                WHERE StudentId = @StudentId;
                """,
                new { StudentId = studentId },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task SaveAllergiesAsync(
        decimal studentId,
        IReadOnlyList<int> allergyItemIds,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0) return;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM StudentAllergies WHERE StudentId = @StudentId;",
                new { StudentId = studentId },
                cancellationToken: cancellationToken));

        if (allergyItemIds.Count == 0) return;

        const string insertSql = """
            INSERT INTO StudentAllergies (StudentId, AllergyItemId, CreatedOn)
            VALUES (@StudentId, @AllergyItemId, GETDATE());
            """;

        foreach (var allergyItemId in allergyItemIds.Distinct())
        {
            if (allergyItemId <= 0) continue;
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    insertSql,
                    new { StudentId = studentId, AllergyItemId = allergyItemId },
                    cancellationToken: cancellationToken));
        }
    }

    public async Task DeleteAllergiesAsync(decimal studentId, CancellationToken cancellationToken = default)
    {
        await SaveAllergiesAsync(studentId, [], cancellationToken);
    }
}
