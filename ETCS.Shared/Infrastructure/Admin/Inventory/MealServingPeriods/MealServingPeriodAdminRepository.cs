using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealServingPeriods;

public sealed class MealServingPeriodAdminRepository : IMealServingPeriodAdminRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealServingPeriodAdminRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    private const string SelectSql = """
        SELECT sp.Id,
            sp.SchoolId,
            sp.StartDate,
            sp.CutoffDate
        """;
    private const string FromSql = "FROM MealPackageServingPeriod sp";
    private const string BaseFilterSql = "1 = 1";
    private const string SearchFilterSql = """
        CAST(sp.SchoolId AS varchar(20)) LIKE '%' + @Search + '%'
        OR CONVERT(varchar(30), sp.StartDate, 120) LIKE '%' + @Search + '%'
        OR CONVERT(varchar(30), sp.CutoffDate, 120) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "sp.Id",
        ["SchoolId"] = "sp.SchoolId",
        ["StartDate"] = "sp.StartDate",
        ["CutoffDate"] = "sp.CutoffDate"
    };

    public async Task<DataTableResponse<MealServingPeriodListDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var baseFilterSql = BaseFilterSql;
        object? extraParameters = null;
        if (request.ScopedSchoolIds is { Count: > 0 })
        {
            baseFilterSql += " AND sp.SchoolId IN @ScopedSchoolIds";
            extraParameters = new { ScopedSchoolIds = request.ScopedSchoolIds };
        }
        else if (request.SchoolId is > 0)
        {
            baseFilterSql += " AND sp.SchoolId = @SchoolId";
            extraParameters = new { SchoolId = request.SchoolId.Value };
        }

        return await QueryPagedAsync<MealServingPeriodListDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql,
            SearchFilterSql,
            SortColumns,
            "sp.StartDate",
            request,
            extraParameters,
            cancellationToken: cancellationToken);
    }

    public async Task<MealServingPeriodSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<MealServingPeriodSaveRequest>(
            new CommandDefinition(
                """
                SELECT Id, SchoolId, StartDate, CutoffDate
                FROM MealPackageServingPeriod
                WHERE Id = @Id;
                """,
                new { Id = id },
                cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveAsync(MealServingPeriodSaveRequest request, CancellationToken cancellationToken = default)
    {
        if (request.SchoolId <= 0)
            return AdminOperationResult.Fail("School is required.");

        if (request.StartDate is not null)
            request.StartDate = request.StartDate.Value.Date;

        if (request.CutoffDate is not null)
            request.CutoffDate = request.CutoffDate.Value.Date;

        if (request.StartDate is not null
            && request.CutoffDate is not null
            && request.CutoffDate < request.StartDate)
        {
            return AdminOperationResult.Fail("Cutoff date cannot be before start date.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var overlapCount = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                """
                SELECT COUNT(1)
                FROM MealPackageServingPeriod
                WHERE SchoolId = @SchoolId
                    AND (@Id = 0 OR Id <> @Id)
                    AND ISNULL(CAST(StartDate AS DATE), '1753-01-01') <= ISNULL(CAST(@CutoffDate AS DATE), '9999-12-31')
                    AND ISNULL(CAST(@StartDate AS DATE), '1753-01-01') <= ISNULL(CAST(CutoffDate AS DATE), '9999-12-31');
                """,
                new
                {
                    request.SchoolId,
                    request.Id,
                    request.StartDate,
                    request.CutoffDate
                },
                cancellationToken: cancellationToken));

        if (overlapCount > 0)
        {
            return AdminOperationResult.Fail("This serving period overlaps an existing period for the same school.");
        }

        if (request.Id > 0)
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE MealPackageServingPeriod
                    SET SchoolId = @SchoolId,
                        StartDate = @StartDate,
                        CutoffDate = @CutoffDate
                    WHERE Id = @Id;
                    """,
                    request,
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Serving period updated successfully.")
                : AdminOperationResult.Fail("Serving period was not updated.");
        }

        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO MealPackageServingPeriod (SchoolId, StartDate, CutoffDate)
                VALUES (@SchoolId, @StartDate, @CutoffDate);
                """,
                request,
                cancellationToken: cancellationToken));
        return inserted > 0
            ? AdminOperationResult.Ok("Serving period added successfully.")
            : AdminOperationResult.Fail("Serving period was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                "DELETE FROM MealPackageServingPeriod WHERE Id = @Id;",
                new { Id = id },
                cancellationToken: cancellationToken));
        return rows > 0
            ? AdminOperationResult.Ok("Record deleted successfully.")
            : AdminOperationResult.Fail("Record was not deleted.");
    }
}
