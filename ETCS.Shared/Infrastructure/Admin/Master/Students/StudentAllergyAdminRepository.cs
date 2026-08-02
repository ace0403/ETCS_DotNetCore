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

    public async Task<IReadOnlyDictionary<decimal, IReadOnlyList<string>>> GetAllergyNamesByStudentIdsAsync(
        IReadOnlyList<decimal> studentIds,
        CancellationToken cancellationToken = default)
    {
        if (studentIds is null || studentIds.Count == 0)
        {
            return new Dictionary<decimal, IReadOnlyList<string>>();
        }

        var distinctIds = studentIds.Where(id => id > 0).Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return new Dictionary<decimal, IReadOnlyList<string>>();
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<(decimal StudentId, string Name)>(
            new CommandDefinition(
                """
                SELECT
                    sa.StudentId,
                    LTRIM(RTRIM(ISNULL(e.EnumValue, ''))) AS Name
                FROM StudentAllergies sa
                INNER JOIN Enums e ON e.Id = sa.AllergyItemId
                WHERE sa.StudentId IN @StudentIds
                  AND ISNULL(e.IsActive, 1) = 1
                ORDER BY sa.StudentId, e.SortOrder, e.EnumValue;
                """,
                new { StudentIds = distinctIds },
                cancellationToken: cancellationToken));

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Name))
            .GroupBy(r => r.StudentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g.Select(x => x.Name.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList());
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
