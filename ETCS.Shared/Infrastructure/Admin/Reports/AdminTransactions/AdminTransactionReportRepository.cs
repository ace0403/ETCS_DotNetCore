using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;

public sealed class AdminTransactionReportRepository : IAdminTransactionReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AdminTransactionReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<AdminTransactionReportRowDto>> GetTransactionsAsync(
        AdminTransactionReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (rows, _) = await QueryTransactionsAsync(filter, start: 0, length: 0, cancellationToken);
        return rows;
    }

    public async Task<AdminTransactionReportPagedResult> GetTransactionsPagedAsync(
        AdminTransactionReportListRequest request,
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

        return new AdminTransactionReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = totalCount,
            Data = rows
        };
    }

    private async Task<(IReadOnlyList<AdminTransactionReportRowDto> Rows, int TotalCount)> QueryTransactionsAsync(
        AdminTransactionReportFilter filter,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var rows = (await dbConnection.QueryAsync<AdminTransactionSpRow>(
            new CommandDefinition(
                "spRptAdmineTransaction_New",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            .Select(r => r.ToDto())
            .ToList();

        var totalCount = parameters.Get<int>("TotalCount");
        return (rows, totalCount);
    }

    private static AdminTransactionReportFilter ToFilter(AdminTransactionReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolCode = request.SchoolCode,
            TerminalCode = request.TerminalCode,
            TransactionType = request.TransactionType,
            StudentCardNo = request.StudentCardNo,
            TransactionId = request.TransactionId
        };

    private static AdminTransactionReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };

    private static DynamicParameters BuildParameters(AdminTransactionReportFilter filter, int start, int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("StartDate", filter.StartDate.Date);
        parameters.Add("EndDate", filter.EndDate.Date);
        parameters.Add("TransactionType", NormalizeTransactionType(filter.TransactionType));
        parameters.Add("customerid", filter.StudentCardNo?.Trim() ?? string.Empty);
        parameters.Add("TerminalCode", filter.TerminalCode?.Trim() ?? string.Empty);
        parameters.Add("SchoolId", filter.SchoolCode?.Trim() ?? string.Empty);
        parameters.Add("TransactionId", filter.TransactionId?.Trim() ?? string.Empty);
        parameters.Add("Start", start);
        parameters.Add("Length", length);
        return parameters;
    }

    private static string NormalizeTransactionType(string? transactionType)
    {
        if (string.IsNullOrWhiteSpace(transactionType))
        {
            return "ALL";
        }

        return transactionType.Trim();
    }
}
