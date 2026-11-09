using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;

public sealed class TerminalSalesSummaryReportRepository : ITerminalSalesSummaryReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public TerminalSalesSummaryReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<TerminalSalesSummaryReportRowDto>> GetSummaryAsync(
        TerminalSalesSummaryReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var (rows, _, _) = await QuerySummaryAsync(filter, start: 0, length: 0, cancellationToken);
        return rows;
    }

    public async Task<TerminalSalesSummaryReportPagedResult> GetSummaryPagedAsync(
        TerminalSalesSummaryReportListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return EmptyPagedResult(request.Draw);
        }

        var filter = ToFilter(request);
        // Length <= 0 means "all rows" for the SP (do not coerce to DefaultPageSize).
        var pageLength = request.Length <= 0 ? 0 : request.Length;

        var (rows, totalCount, totals) = await QuerySummaryAsync(
            filter,
            request.Start,
            pageLength,
            cancellationToken);

        return new TerminalSalesSummaryReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = totalCount,
            RecordsFiltered = totalCount,
            Data = rows,
            Totals = totals
        };
    }

    private async Task<(
        IReadOnlyList<TerminalSalesSummaryReportRowDto> Rows,
        int TotalCount,
        TerminalSalesSummaryReportTotalsDto Totals)> QuerySummaryAsync(
        TerminalSalesSummaryReportFilter filter,
        int start,
        int length,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = BuildParameters(filter, start, length);
        parameters.Add("TotalCount", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("StudentsCountTotal", dbType: DbType.Int32, direction: ParameterDirection.Output);
        parameters.Add("StudentCardPurchaseTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("CashPurchaseTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("CreditCardPurchaseTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("StudentCardManualTopupTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("StudentCardUndoTopupTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("OnlineStudentCardTopupTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);
        parameters.Add("UndoCashPurchaseTotal", dbType: DbType.Decimal, direction: ParameterDirection.Output);

        var rows = (await dbConnection.QueryAsync<TerminalSalesSummarySpRow>(
            new CommandDefinition(
                "spEventTransSummary1_New",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken)))
            .Select(r => r.ToDto())
            .ToList();

        var totalCount = parameters.Get<int?>("TotalCount") ?? 0;
        var totals = new TerminalSalesSummaryReportTotalsDto
        {
            StudentsCount = parameters.Get<int?>("StudentsCountTotal") ?? 0,
            StudentCardPurchase = parameters.Get<decimal?>("StudentCardPurchaseTotal") ?? 0m,
            CashPurchase = parameters.Get<decimal?>("CashPurchaseTotal") ?? 0m,
            CreditCardPurchase = parameters.Get<decimal?>("CreditCardPurchaseTotal") ?? 0m,
            StudentCardManualTopup = parameters.Get<decimal?>("StudentCardManualTopupTotal") ?? 0m,
            StudentCardUndoTopup = parameters.Get<decimal?>("StudentCardUndoTopupTotal") ?? 0m,
            OnlineStudentCardTopup = parameters.Get<decimal?>("OnlineStudentCardTopupTotal") ?? 0m,
            UndoCashPurchase = parameters.Get<decimal?>("UndoCashPurchaseTotal") ?? 0m
        };

        return (rows, totalCount, totals);
    }

    private static TerminalSalesSummaryReportFilter ToFilter(TerminalSalesSummaryReportListRequest request) =>
        new()
        {
            StartDate = request.StartDate!.Value.Date,
            EndDate = request.EndDate!.Value.Date,
            SchoolCode = request.SchoolCode,
            SchoolCodesCsv = request.SchoolCodesCsv,
            TerminalCode = request.TerminalCode,
            TransactionType = request.TransactionType
        };

    private static TerminalSalesSummaryReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = [],
            Totals = new TerminalSalesSummaryReportTotalsDto()
        };

    private static DynamicParameters BuildParameters(TerminalSalesSummaryReportFilter filter, int start, int length)
    {
        var parameters = new DynamicParameters();
        parameters.Add("StartDate", filter.StartDate.Date);
        parameters.Add("EndDate", filter.EndDate.Date);
        parameters.Add("EventId", string.Empty);
        parameters.Add("TransectionType", NormalizeTransactionType(filter.TransactionType));
        parameters.Add("SchoolCode", filter.SchoolCode?.Trim() ?? string.Empty);
        parameters.Add("SchoolCodesCsv", filter.SchoolCodesCsv?.Trim() ?? string.Empty);
        parameters.Add("TerminalCode", filter.TerminalCode?.Trim() ?? string.Empty);
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

    private sealed class TerminalSalesSummarySpRow
    {
        public string TerminalCode { get; init; } = string.Empty;
        public string TerminalName { get; init; } = string.Empty;
        public string Date { get; init; } = string.Empty;
        public int StudentsCount { get; init; }
        public decimal StudentCardPurchase { get; init; }
        public decimal CashPurchase { get; init; }
        public decimal CreditCardPurchase { get; init; }
        public decimal StudentCardManualTopup { get; init; }
        public decimal StudentCardUndoTopup { get; init; }
        public decimal OnlineStudentCardTopup { get; init; }
        public decimal UndoCashPurchase { get; init; }

        public TerminalSalesSummaryReportRowDto ToDto() =>
            new()
            {
                TerminalCode = TerminalCode,
                TerminalName = TerminalName,
                Date = Date,
                StudentsCount = StudentsCount,
                StudentCardPurchase = StudentCardPurchase,
                CashPurchase = CashPurchase,
                CreditCardPurchase = CreditCardPurchase,
                StudentCardManualTopup = StudentCardManualTopup,
                StudentCardUndoTopup = StudentCardUndoTopup,
                OnlineStudentCardTopup = OnlineStudentCardTopup,
                UndoCashPurchase = UndoCashPurchase
            };
    }
}
