using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;

public sealed class MealOrderMealDbReportRepository : IMealOrderMealDbReportRepository
{
    private const string SchoolsSql = """
        SELECT
            CAST(SchoolID AS VARCHAR(10)) AS Id,
            LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
        FROM SchoolInfo
        WHERE SchoolID IS NOT NULL
        ORDER BY SchoolName;
        """;

    private const string StudentsSql = """
        SELECT
            sl.UserId AS StudentId,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudCode,
            LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS StudStd,
            LTRIM(RTRIM(ISNULL(sl.StudDiv, ''))) AS StudDiv,
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS StudFullName
        FROM StudentLogin sl
        WHERE sl.UserId IN @StudentIds;
        """;

    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly IDbConnectionFactory _connectionFactory;

    public MealOrderMealDbReportRepository(
        IMealDbConnectionFactory mealDbConnectionFactory,
        IDbConnectionFactory connectionFactory)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<MealOrderSchoolLookupDto>> GetSchoolsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<MealOrderSchoolLookupDto>(
            new CommandDefinition(SchoolsSql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MealOrderReportRowDto>> GetOrdersAsync(
        MealOrderReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (rows, _) = await QueryOrdersAsync(filter, start: 0, length: 0, cancellationToken);
        return rows;
    }

    public async Task<MealOrderReportPagedResult> GetOrdersPagedAsync(
        MealOrderReportListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return EmptyPagedResult(request.Draw);
        }

        var filter = ToFilter(request);
        var (rows, totalCount) = await QueryOrdersAsync(
            filter,
            request.Start,
            request.PageSize,
            cancellationToken);

        return new MealOrderReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = totalCount,
            Data = rows
        };
    }

    private async Task<(IReadOnlyList<MealOrderReportRowDto> Rows, int TotalCount)> QueryOrdersAsync(
        MealOrderReportFilter filter,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var mealConnection = _mealDbConnectionFactory.CreateConnection();
        var mealDbConnection = (DbConnection)mealConnection;
        await mealDbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var spRows = (await mealDbConnection.QueryAsync<MealOrderMealDbSpRow>(
            new CommandDefinition(
                "spMealOrderSummary_MealDB_New",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            .ToList();

        var totalCount = parameters.Get<int>("TotalCount");
        if (spRows.Count == 0)
        {
            return ([], totalCount);
        }

        var studentIds = spRows.Select(r => r.StudentId).Distinct().ToArray();
        var students = await LoadStudentsAsync(studentIds, cancellationToken);
        var studentMap = students.ToDictionary(s => s.StudentId);

        var rows = spRows
            .Select(r => r.ToDto(studentMap.GetValueOrDefault(r.StudentId)))
            .ToList();

        return (rows, totalCount);
    }

    private async Task<IReadOnlyList<MealOrderStudentRow>> LoadStudentsAsync(
        int[] studentIds,
        CancellationToken cancellationToken)
    {
        if (studentIds.Length == 0)
        {
            return [];
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<MealOrderStudentRow>(
            new CommandDefinition(
                StudentsSql,
                new { StudentIds = studentIds },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static MealOrderReportFilter ToFilter(MealOrderReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolId = request.SchoolId
        };

    private static MealOrderReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };

    private static DynamicParameters BuildParameters(MealOrderReportFilter filter, int start, int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("startdate", filter.StartDate.Date);
        parameters.Add("enddate", filter.EndDate.Date);
        parameters.Add("SchoolId", filter.SchoolId?.Trim() ?? string.Empty);
        parameters.Add("Start", start);
        parameters.Add("Length", length);
        return parameters;
    }

    private sealed class MealOrderMealDbSpRow
    {
        public DateTime? OrderDate { get; init; }
        public int StudentId { get; init; }
        public string? PaymentStatus { get; init; }
        public string? Category { get; init; }
        public string? Choice { get; init; }
        public DateTime? DeliveryDate { get; init; }
        public string? Day { get; init; }
        public string? Items { get; init; }

        public MealOrderReportRowDto ToDto(MealOrderStudentRow? student) =>
            new()
            {
                OrderDate = FormatDate(OrderDate),
                StudCode = student?.StudCode?.Trim() ?? string.Empty,
                StudStd = student?.StudStd?.Trim() ?? string.Empty,
                StudDiv = student?.StudDiv?.Trim() ?? string.Empty,
                StudFullName = student?.StudFullName?.Trim() ?? string.Empty,
                PaymentStatus = PaymentStatus?.Trim() ?? string.Empty,
                Category = Category?.Trim() ?? string.Empty,
                Choice = Choice?.Trim() ?? string.Empty,
                DeliveryDate = FormatDate(DeliveryDate),
                Day = Day?.Trim() ?? string.Empty,
                Items = Items?.Trim() ?? string.Empty
            };

        private static string FormatDate(DateTime? value) =>
            value.HasValue
                ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : string.Empty;
    }

    private sealed class MealOrderStudentRow
    {
        public int StudentId { get; init; }
        public string? StudCode { get; init; }
        public string? StudStd { get; init; }
        public string? StudDiv { get; init; }
        public string? StudFullName { get; init; }
    }
}
