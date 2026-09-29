using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

public sealed class MealOrderPaymentMealDbReportRepository : IMealOrderPaymentMealDbReportRepository
{
    private const string PackageName = "PACKAGE UNKNOWN";

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
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS StudFullName,
            LTRIM(RTRIM(ISNULL(si.SchoolName, ''))) AS SchoolName
        FROM StudentLogin sl
        LEFT JOIN SchoolInfo si ON si.SchoolID = sl.StudSchoolId
        WHERE sl.UserId IN @StudentIds;
        """;

    private const string ResolveStudentIdByCustomerIdSql = """
        SELECT TOP 1 sl.UserId
        FROM StudentLogin sl
        WHERE LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = @StudentCardNo;
        """;

    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly IDbConnectionFactory _connectionFactory;

    public MealOrderPaymentMealDbReportRepository(
        IMealDbConnectionFactory mealDbConnectionFactory,
        IDbConnectionFactory connectionFactory)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<MealOrderPaymentSchoolLookupDto>> GetSchoolsAsync(
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<MealOrderPaymentSchoolLookupDto>(
            new CommandDefinition(SchoolsSql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<MealOrderPaymentReportRowDto>> GetOrdersAsync(
        MealOrderPaymentReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (rows, _) = await QueryOrdersAsync(filter, start: 0, length: 0, cancellationToken);
        return rows;
    }

    public async Task<MealOrderPaymentReportPagedResult> GetOrdersPagedAsync(
        MealOrderPaymentReportListRequest request,
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

        return new MealOrderPaymentReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = totalCount,
            Data = rows
        };
    }

    private async Task<(IReadOnlyList<MealOrderPaymentReportRowDto> Rows, int TotalCount)> QueryOrdersAsync(
        MealOrderPaymentReportFilter filter,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        var studentIdResult = await ResolveStudentIdAsync(filter.StudentCardNo, cancellationToken);
        if (studentIdResult.NotFound)
        {
            return ([], 0);
        }

        using var mealConnection = _mealDbConnectionFactory.CreateConnection();
        var mealDbConnection = (DbConnection)mealConnection;
        await mealDbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, studentIdResult.StudentId, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var spRows = (await mealDbConnection.QueryAsync<MealOrderPaymentMealDbSpRow>(
            new CommandDefinition(
                "spMealOrderPaymentSummary_MealDB_New",
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

    private async Task<(int StudentId, bool NotFound)> ResolveStudentIdAsync(
        string? studentCardNo,
        CancellationToken cancellationToken)
    {
        var cardNo = studentCardNo?.Trim() ?? string.Empty;
        if (cardNo.Length == 0)
        {
            return (0, false);
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var userId = await dbConnection.QuerySingleOrDefaultAsync<int?>(
            new CommandDefinition(
                ResolveStudentIdByCustomerIdSql,
                new { StudentCardNo = cardNo },
                cancellationToken: cancellationToken));

        return userId is null or 0
            ? (0, true)
            : (userId.Value, false);
    }

    private async Task<IReadOnlyList<MealOrderPaymentStudentRow>> LoadStudentsAsync(
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

        var rows = await dbConnection.QueryAsync<MealOrderPaymentStudentRow>(
            new CommandDefinition(
                StudentsSql,
                new { StudentIds = studentIds },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static MealOrderPaymentReportFilter ToFilter(MealOrderPaymentReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolId = request.SchoolId,
            SchoolIdsCsv = request.SchoolIdsCsv,
            MealSessionId = request.MealSessionId,
            MealTypeId = request.MealTypeId,
            TransactionId = request.TransactionId,
            StudentCardNo = request.StudentCardNo
        };

    private static MealOrderPaymentReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };

    private static DynamicParameters BuildParameters(
        MealOrderPaymentReportFilter filter,
        int studentId,
        int start,
        int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("startdate", filter.StartDate.Date);
        parameters.Add("enddate", filter.EndDate.Date);
        parameters.Add("SchoolId", filter.SchoolId?.Trim() ?? string.Empty);
        parameters.Add("SchoolIdsCsv", filter.SchoolIdsCsv?.Trim() ?? string.Empty);
        parameters.Add("TransactionId", filter.TransactionId?.Trim() ?? string.Empty);
        parameters.Add("StudentId", studentId);
        parameters.Add("Start", start);
        parameters.Add("Length", length);
        return parameters;
    }

    private sealed class MealOrderPaymentMealDbSpRow
    {
        public DateTime? OrderDate { get; init; }
        public int StudentId { get; init; }
        public string? PaymentStatus { get; init; }
        public string? MealSession { get; init; }
        public string? TransactionId { get; init; }
        public decimal Amount { get; init; }
        public DateTime? DeliveryDate { get; init; }
        public string? Day { get; init; }
        public string? Items { get; init; }
        public bool HasAllergenConsent { get; init; }
        public string? AllergenItemText { get; init; }

        public MealOrderPaymentReportRowDto ToDto(MealOrderPaymentStudentRow? student)
        {
            var studentName = student?.StudFullName?.Trim() ?? string.Empty;
            var hasConsent = HasAllergenConsent;
            return new MealOrderPaymentReportRowDto
            {
                OrderDate = FormatDate(OrderDate),
                StudCode = student?.StudCode?.Trim() ?? string.Empty,
                StudStd = student?.StudStd?.Trim() ?? string.Empty,
                StudFullName = studentName,
                TransactionId = TransactionId?.Trim() ?? string.Empty,
                Amount = Amount,
                Package = PackageName,
                SchoolName = student?.SchoolName?.Trim() ?? string.Empty,
                HasAllergenConsent = hasConsent,
                AllergenConsentText = hasConsent
                    ? ETCS.Shared.Infrastructure.Meals.Menu.AllergenConsentText.FormatStoredConsentDisplay(
                        studentName,
                        AllergenItemText,
                        "meal")
                    : string.Empty
            };
        }

        private static string FormatDate(DateTime? value) =>
            value.HasValue
                ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : string.Empty;
    }

    private sealed class MealOrderPaymentStudentRow
    {
        public int StudentId { get; init; }
        public string? StudCode { get; init; }
        public string? StudStd { get; init; }
        public string? StudDiv { get; init; }
        public string? StudFullName { get; init; }
        public string? SchoolName { get; init; }
    }
}
