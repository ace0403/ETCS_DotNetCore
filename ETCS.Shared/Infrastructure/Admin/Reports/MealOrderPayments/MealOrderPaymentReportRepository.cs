using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

public sealed class MealOrderPaymentReportRepository : IMealOrderPaymentReportRepository
{
    private const string SchoolsSql = """
        SELECT
            CAST(SchoolID AS VARCHAR(10)) AS Id,
            LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
        FROM SchoolInfo
        WHERE SchoolID IS NOT NULL
        ORDER BY SchoolName;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public MealOrderPaymentReportRepository(IDbConnectionFactory connectionFactory)
    {
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
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var rows = (await dbConnection.QueryAsync<MealOrderPaymentReportSpRow>(
            new CommandDefinition(
                "spMealOrderPaymentSummary_New",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            .Select(r => r.ToDto())
            .ToList();

        var totalCount = parameters.Get<int>("TotalCount");
        return (rows, totalCount);
    }

    private static MealOrderPaymentReportFilter ToFilter(MealOrderPaymentReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolId = request.SchoolId,
            SchoolIdsCsv = request.SchoolIdsCsv
        };

    private static MealOrderPaymentReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };

    private static DynamicParameters BuildParameters(MealOrderPaymentReportFilter filter, int start, int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("startdate", filter.StartDate.Date);
        parameters.Add("enddate", filter.EndDate.Date);
        parameters.Add("SchoolId", filter.SchoolId?.Trim() ?? string.Empty);
        parameters.Add("SchoolIdsCsv", filter.SchoolIdsCsv?.Trim() ?? string.Empty);
        parameters.Add("Start", start);
        parameters.Add("Length", length);
        return parameters;
    }

    private sealed class MealOrderPaymentReportSpRow
    {
        public DateTime? OrderDate { get; init; }
        public string? StudCode { get; init; }
        public string? StudStd { get; init; }
        public string? StudDiv { get; init; }
        public string? StudFullName { get; init; }
        public string? PaymentStatus { get; init; }
        public string? TransactionId { get; init; }
        public decimal Amount { get; init; }
        public DateTime? DeliveryDate { get; init; }
        public string? Day { get; init; }
        public string? Items { get; init; }

        public MealOrderPaymentReportRowDto ToDto() =>
            new()
            {
                OrderDate = FormatDate(OrderDate),
                StudCode = StudCode?.Trim() ?? string.Empty,
                StudStd = StudStd?.Trim() ?? string.Empty,
                StudDiv = StudDiv?.Trim() ?? string.Empty,
                StudFullName = StudFullName?.Trim() ?? string.Empty,
                PaymentStatus = PaymentStatus?.Trim() ?? string.Empty,
                TransactionId = TransactionId?.Trim() ?? string.Empty,
                Amount = Amount,
                DeliveryDate = FormatDate(DeliveryDate),
                Day = Day?.Trim() ?? string.Empty,
                Items = Items?.Trim() ?? string.Empty
            };

        private static string FormatDate(DateTime? value) =>
            value.HasValue
                ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
                : string.Empty;
    }
}
