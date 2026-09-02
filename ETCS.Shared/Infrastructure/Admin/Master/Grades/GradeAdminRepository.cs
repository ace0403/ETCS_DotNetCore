using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Master.Grades;

public sealed class GradeAdminRepository : IGradeAdminRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public GradeAdminRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectSql = """
        SELECT Id,
               LTRIM(RTRIM(Grade)) AS Grade
        """;

    private const string FromSql = "FROM SchoolGrades";

    private const string SearchFilterSql = "LTRIM(RTRIM(ISNULL(Grade, ''))) LIKE '%' + @Search + '%'";

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "Id",
        ["Grade"] = "Grade"
    };

    public async Task<DataTableResponse<GradeListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<GradeListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql: null,
            SearchFilterSql,
            SortColumns,
            "Grade",
            request,
            cancellationToken: cancellationToken);
    }

    public async Task<GradeSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<GradeSaveRequest>(
            new CommandDefinition(
                """
                SELECT Id,
                       LTRIM(RTRIM(Grade)) AS Grade
                FROM SchoolGrades
                WHERE Id = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveAsync(GradeSaveRequest request, CancellationToken cancellationToken = default)
    {
        var grade = request.Grade.Trim();
        if (string.IsNullOrWhiteSpace(grade))
        {
            return AdminOperationResult.Fail("Grade is required.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        if (await HasDuplicateAsync(dbConnection, grade, request.Id, cancellationToken))
        {
            return AdminOperationResult.Fail("A grade with the same name already exists.");
        }

        if (request.Id > 0)
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE SchoolGrades
                    SET Grade = @Grade
                    WHERE Id = @Id;
                    """,
                    new
                    {
                        request.Id,
                        Grade = grade
                    },
                    cancellationToken: cancellationToken));

            return rows > 0
                ? AdminOperationResult.Ok("Grade updated successfully.")
                : AdminOperationResult.Fail("Grade was not updated.");
        }

        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO SchoolGrades (Grade, SchoolCode)
                VALUES (@Grade, NULL);
                """,
                new { Grade = grade },
                cancellationToken: cancellationToken));

        return inserted > 0
            ? AdminOperationResult.Ok("Grade added successfully.")
            : AdminOperationResult.Fail("Grade was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return AdminOperationResult.Fail("Id is required.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        try
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM SchoolGrades WHERE Id = @Id;",
                    new { Id = id },
                    cancellationToken: cancellationToken));

            return rows > 0
                ? AdminOperationResult.Ok("Record deleted successfully.")
                : AdminOperationResult.Fail("Record was not deleted.");
        }
        catch
        {
            return AdminOperationResult.Fail("Record could not be deleted. It may be in use.");
        }
    }

    private static async Task<bool> HasDuplicateAsync(
        DbConnection connection,
        string grade,
        int excludeId,
        CancellationToken cancellationToken)
    {
        var count = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM SchoolGrades
                WHERE LTRIM(RTRIM(Grade)) = @Grade
                  AND Id <> @ExcludeId;
                """,
                new
                {
                    Grade = grade,
                    ExcludeId = excludeId
                },
                cancellationToken: cancellationToken));

        return count > 0;
    }
}
