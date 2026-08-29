using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Schools.Calendar;

public sealed class SchoolCalendarRepository : ISchoolCalendarRepository
{
    private static readonly (byte DayOfWeek, byte DayStatus)[] DefaultWeekly =
    [
        (0, (byte)SchoolDayStatus.Holiday),  // Sunday
        (1, (byte)SchoolDayStatus.FullDay),  // Monday
        (2, (byte)SchoolDayStatus.FullDay),  // Tuesday
        (3, (byte)SchoolDayStatus.FullDay),  // Wednesday
        (4, (byte)SchoolDayStatus.FullDay),  // Thursday
        (5, (byte)SchoolDayStatus.HalfDay),  // Friday
        (6, (byte)SchoolDayStatus.Holiday)   // Saturday
    ];

    private readonly IMealDbConnectionFactory _connectionFactory;

    public SchoolCalendarRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task EnsureWeeklyDefaultsAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            return;
        }

        const string sql = """
            INSERT INTO [SchoolWeeklySchedule] (SchoolId, DayOfWeek, DayStatus)
            SELECT @SchoolId, v.DayOfWeek, v.DayStatus
            FROM (VALUES
                (@D0, @S0), (@D1, @S1), (@D2, @S2), (@D3, @S3),
                (@D4, @S4), (@D5, @S5), (@D6, @S6)
            ) AS v (DayOfWeek, DayStatus)
            WHERE NOT EXISTS (
                SELECT 1
                FROM [SchoolWeeklySchedule] w
                WHERE w.SchoolId = @SchoolId
                  AND w.DayOfWeek = v.DayOfWeek
            );
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SchoolId = schoolId,
                D0 = DefaultWeekly[0].DayOfWeek,
                S0 = DefaultWeekly[0].DayStatus,
                D1 = DefaultWeekly[1].DayOfWeek,
                S1 = DefaultWeekly[1].DayStatus,
                D2 = DefaultWeekly[2].DayOfWeek,
                S2 = DefaultWeekly[2].DayStatus,
                D3 = DefaultWeekly[3].DayOfWeek,
                S3 = DefaultWeekly[3].DayStatus,
                D4 = DefaultWeekly[4].DayOfWeek,
                S4 = DefaultWeekly[4].DayStatus,
                D5 = DefaultWeekly[5].DayOfWeek,
                S5 = DefaultWeekly[5].DayStatus,
                D6 = DefaultWeekly[6].DayOfWeek,
                S6 = DefaultWeekly[6].DayStatus
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<SchoolWeeklyDayDto>> GetWeeklyAsync(
        int schoolId,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            return [];
        }

        await EnsureWeeklyDefaultsAsync(schoolId, cancellationToken);

        const string sql = """
            SELECT DayOfWeek, DayStatus
            FROM [SchoolWeeklySchedule]
            WHERE SchoolId = @SchoolId
            ORDER BY DayOfWeek;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SchoolWeeklyDayDto>(new CommandDefinition(
            sql,
            new { SchoolId = schoolId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<AdminOperationResult> SaveWeeklyAsync(
        int schoolId,
        IReadOnlyList<SchoolWeeklyDaySaveRequest> days,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            return AdminOperationResult.Fail("School is required.");
        }

        if (days is null || days.Count != 7)
        {
            return AdminOperationResult.Fail("Weekly schedule must include all 7 days.");
        }

        var byDay = days
            .GroupBy(d => d.DayOfWeek)
            .ToDictionary(g => g.Key, g => g.First().DayStatus);

        for (byte d = 0; d <= 6; d++)
        {
            if (!byDay.ContainsKey(d))
            {
                return AdminOperationResult.Fail("Weekly schedule must include all 7 days.");
            }

            if (!IsValidStatus(byDay[d]))
            {
                return AdminOperationResult.Fail("Invalid day status.");
            }
        }

        const string sql = """
            MERGE [SchoolWeeklySchedule] AS t
            USING (VALUES
                (@SchoolId, @D0, @S0),
                (@SchoolId, @D1, @S1),
                (@SchoolId, @D2, @S2),
                (@SchoolId, @D3, @S3),
                (@SchoolId, @D4, @S4),
                (@SchoolId, @D5, @S5),
                (@SchoolId, @D6, @S6)
            ) AS s (SchoolId, DayOfWeek, DayStatus)
            ON t.SchoolId = s.SchoolId AND t.DayOfWeek = s.DayOfWeek
            WHEN MATCHED THEN
                UPDATE SET DayStatus = s.DayStatus
            WHEN NOT MATCHED THEN
                INSERT (SchoolId, DayOfWeek, DayStatus)
                VALUES (s.SchoolId, s.DayOfWeek, s.DayStatus);
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                SchoolId = schoolId,
                D0 = (byte)0,
                S0 = byDay[0],
                D1 = (byte)1,
                S1 = byDay[1],
                D2 = (byte)2,
                S2 = byDay[2],
                D3 = (byte)3,
                S3 = byDay[3],
                D4 = (byte)4,
                S4 = byDay[4],
                D5 = (byte)5,
                S5 = byDay[5],
                D6 = (byte)6,
                S6 = byDay[6]
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return AdminOperationResult.Ok("Weekly schedule saved.");
    }

    public async Task<IReadOnlyList<SchoolCalendarExceptionDto>> GetExceptionsAsync(
        int schoolId,
        DateTime? fromDateInclusive,
        DateTime? toDateExclusive,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                Id,
                SchoolId,
                ExceptionDate,
                DayStatus,
                Title = ISNULL(Title, ''),
                Notes
            FROM [SchoolCalendarExceptions]
            WHERE SchoolId = @SchoolId
              AND (@FromDate IS NULL OR ExceptionDate >= @FromDate)
              AND (@ToDate IS NULL OR ExceptionDate < @ToDate)
            ORDER BY ExceptionDate;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<SchoolCalendarExceptionDto>(new CommandDefinition(
            sql,
            new
            {
                SchoolId = schoolId,
                FromDate = fromDateInclusive?.Date,
                ToDate = toDateExclusive?.Date
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<DataTableResponse<SchoolCalendarExceptionDto>> GetExceptionsPagedAsync(
        int? schoolId,
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var baseFilterSql = "1 = 1";
        object? extraParameters = null;
        if (schoolId is > 0)
        {
            baseFilterSql += " AND e.SchoolId = @SchoolId";
            extraParameters = new { SchoolId = schoolId.Value };
        }

        const string selectSql = """
            SELECT
                e.Id,
                e.SchoolId,
                e.ExceptionDate,
                e.DayStatus,
                Title = ISNULL(e.Title, ''),
                e.Notes
            """;
        const string fromSql = "FROM [SchoolCalendarExceptions] e";
        const string searchFilterSql = """
            ISNULL(e.Title, '') LIKE '%' + @Search + '%'
            OR ISNULL(e.Notes, '') LIKE '%' + @Search + '%'
            OR CONVERT(varchar(30), e.ExceptionDate, 23) LIKE '%' + @Search + '%'
            """;

        var sortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Id"] = "e.Id",
            ["SchoolId"] = "e.SchoolId",
            ["ExceptionDate"] = "e.ExceptionDate",
            ["DayStatus"] = "e.DayStatus",
            ["Title"] = "e.Title"
        };

        return await QueryPagedAsync<SchoolCalendarExceptionDto>(
            dbConnection,
            selectSql,
            fromSql,
            baseFilterSql,
            searchFilterSql,
            sortColumns,
            "e.ExceptionDate",
            request,
            extraParameters,
            cancellationToken: cancellationToken);
    }

    public async Task<SchoolCalendarExceptionDto?> GetExceptionByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        const string sql = """
            SELECT
                Id,
                SchoolId,
                ExceptionDate,
                DayStatus,
                Title = ISNULL(Title, ''),
                Notes
            FROM [SchoolCalendarExceptions]
            WHERE Id = @Id;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<SchoolCalendarExceptionDto>(new CommandDefinition(
            sql,
            new { Id = id },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<AdminOperationResult> SaveExceptionAsync(
        SchoolCalendarExceptionSaveRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SchoolId <= 0)
        {
            return AdminOperationResult.Fail("School is required.");
        }

        if (!IsValidStatus(request.DayStatus))
        {
            return AdminOperationResult.Fail("Invalid day status.");
        }

        var title = (request.Title ?? string.Empty).Trim();
        if (title.Length > 100)
        {
            return AdminOperationResult.Fail("Title must be 100 characters or fewer.");
        }

        if (request.DayStatus == (byte)SchoolDayStatus.Holiday && string.IsNullOrWhiteSpace(title))
        {
            title = "Holiday";
        }

        var exceptionDate = request.ExceptionDate.Date;
        var notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();

        using var connection = _connectionFactory.CreateConnection();

        var duplicate = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            """
            SELECT COUNT(1)
            FROM [SchoolCalendarExceptions]
            WHERE SchoolId = @SchoolId
              AND ExceptionDate = @ExceptionDate
              AND (@Id = 0 OR Id <> @Id);
            """,
            new { request.Id, request.SchoolId, ExceptionDate = exceptionDate },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (duplicate > 0)
        {
            return AdminOperationResult.Fail("An exception already exists for this school and date.");
        }

        if (request.Id > 0)
        {
            var updated = await connection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE [SchoolCalendarExceptions]
                SET SchoolId = @SchoolId,
                    ExceptionDate = @ExceptionDate,
                    DayStatus = @DayStatus,
                    Title = @Title,
                    Notes = @Notes
                WHERE Id = @Id;
                """,
                new
                {
                    request.Id,
                    request.SchoolId,
                    ExceptionDate = exceptionDate,
                    request.DayStatus,
                    Title = title,
                    Notes = notes
                },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

            return updated > 0
                ? AdminOperationResult.Ok("Holiday saved.")
                : AdminOperationResult.Fail("Holiday not found.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO [SchoolCalendarExceptions]
                (SchoolId, ExceptionDate, DayStatus, Title, Notes, CreatedOn)
            VALUES
                (@SchoolId, @ExceptionDate, @DayStatus, @Title, @Notes, GETDATE());
            """,
            new
            {
                request.SchoolId,
                ExceptionDate = exceptionDate,
                request.DayStatus,
                Title = title,
                Notes = notes
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return AdminOperationResult.Ok("Holiday saved.");
    }

    public async Task<AdminOperationResult> DeleteExceptionAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return AdminOperationResult.Fail("Invalid holiday.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var deleted = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM [SchoolCalendarExceptions] WHERE Id = @Id;",
            new { Id = id },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return deleted > 0
            ? AdminOperationResult.Ok("Holiday deleted.")
            : AdminOperationResult.Fail("Holiday not found.");
    }

    public async Task<IReadOnlyList<SchoolDayInfo>> ResolveRangeAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0 || fromDateInclusive.Date >= toDateExclusive.Date)
        {
            return [];
        }

        await EnsureWeeklyDefaultsAsync(schoolId, cancellationToken);

        var weekly = await GetWeeklyAsync(schoolId, cancellationToken);
        var weeklyMap = weekly.ToDictionary(x => x.DayOfWeek, x => (SchoolDayStatus)x.DayStatus);

        var exceptions = await GetExceptionsAsync(schoolId, fromDateInclusive, toDateExclusive, cancellationToken);
        var exceptionMap = exceptions.ToDictionary(
            x => x.ExceptionDate.Date,
            x => x);

        var result = new List<SchoolDayInfo>();
        for (var date = fromDateInclusive.Date; date < toDateExclusive.Date; date = date.AddDays(1))
        {
            if (exceptionMap.TryGetValue(date, out var exception))
            {
                result.Add(new SchoolDayInfo(
                    date,
                    (SchoolDayStatus)exception.DayStatus,
                    string.IsNullOrWhiteSpace(exception.Title) ? null : exception.Title.Trim(),
                    IsException: true));
                continue;
            }

            var dow = (byte)date.DayOfWeek;
            var status = weeklyMap.TryGetValue(dow, out var weeklyStatus)
                ? weeklyStatus
                : SchoolDayStatus.FullDay;

            result.Add(new SchoolDayInfo(date, status, Title: null, IsException: false));
        }

        return result;
    }

    private static bool IsValidStatus(byte status) =>
        status is (byte)SchoolDayStatus.Holiday
            or (byte)SchoolDayStatus.FullDay
            or (byte)SchoolDayStatus.HalfDay;
}
