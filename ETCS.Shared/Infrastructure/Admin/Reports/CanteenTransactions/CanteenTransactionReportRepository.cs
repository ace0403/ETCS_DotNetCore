using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;

public sealed class CanteenTransactionReportRepository : ICanteenTransactionReportRepository
{
    private const string SchoolsSql = """
        SELECT
            LTRIM(RTRIM(ISNULL(Schoolcode, ''))) AS Code,
            LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
        FROM SchoolInfo
        WHERE LTRIM(RTRIM(ISNULL(Schoolcode, ''))) <> ''
        ORDER BY SchoolName;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public CanteenTransactionReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<SchoolCodeLookupDto>> GetSchoolsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<SchoolCodeLookupDto>(
            new CommandDefinition(SchoolsSql, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TerminalLookupDto>> GetBranchesAsync(
        string? schoolCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schoolCode))
        {
            return [];
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<TerminalLookupDto>(
            new CommandDefinition(
                "spGetTerminalInfo",
                new { SchoolCode = schoolCode.Trim() },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<CanteenTransactionReportRowDto>> GetTransactionsAsync(
        CanteenTransactionReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (rows, _) = await QueryTransactionsAsync(filter, start: 0, length: 0, cancellationToken);
        return rows;
    }

    public async Task<CanteenTransactionReportPagedResult> GetTransactionsPagedAsync(
        CanteenTransactionReportListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return EmptyPagedResult(request.Draw);
        }

        var filter = ToFilter(request);
        var (rows, totalCount) = await QueryTransactionsAsync(
            filter,
            request.Start,
            request.PageSize,
            cancellationToken);

        return new CanteenTransactionReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = totalCount,
            Data = rows
        };
    }

    private async Task<(IReadOnlyList<CanteenTransactionReportRowDto> Rows, int TotalCount)> QueryTransactionsAsync(
        CanteenTransactionReportFilter filter,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var rows = (await dbConnection.QueryAsync<CanteenTransactionSpRow>(
            new CommandDefinition(
                "spCanteentranSummary_New",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            .Select(r => r.ToDto())
            .ToList();

        var totalCount = parameters.Get<int>("TotalCount");

        return (rows, totalCount);
    }

    private static CanteenTransactionReportFilter ToFilter(CanteenTransactionReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolCode = request.SchoolCode,
            SchoolCodesCsv = request.SchoolCodesCsv,
            Branch = request.Branch,
            TransactionType = request.TransactionType,
            StudentCardNo = request.StudentCardNo
        };

    private static CanteenTransactionReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };

    private static DynamicParameters BuildParameters(CanteenTransactionReportFilter filter, int start, int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("startdate", filter.StartDate.Date);
        parameters.Add("enddate", filter.EndDate.Date);
        parameters.Add("transaciontype", filter.TransactionType?.Trim() ?? string.Empty);
        parameters.Add("customerid", filter.StudentCardNo?.Trim() ?? string.Empty);
        parameters.Add("SchoolId", filter.SchoolCode?.Trim() ?? string.Empty);
        parameters.Add("SchoolCodesCsv", filter.SchoolCodesCsv?.Trim() ?? string.Empty);
        parameters.Add("branch", filter.Branch?.Trim() ?? string.Empty);
        parameters.Add("Start", start);
        parameters.Add("Length", length);
        return parameters;
    }
}
