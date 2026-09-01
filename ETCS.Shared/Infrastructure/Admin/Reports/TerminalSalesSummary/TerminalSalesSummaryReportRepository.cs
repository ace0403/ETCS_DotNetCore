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

        var (rows, _) = await QuerySummaryAsync(filter, start: 0, length: 0, cancellationToken);

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

        var (rows, totalCount) = await QuerySummaryAsync(

            filter,

            request.Start,

            request.PageSize,

            cancellationToken);



        return new TerminalSalesSummaryReportPagedResult

        {

            Draw = request.Draw,

            RecordsTotal = totalCount,

            RecordsFiltered = totalCount,

            Data = rows

        };

    }



    private async Task<(IReadOnlyList<TerminalSalesSummaryReportRowDto> Rows, int TotalCount)> QuerySummaryAsync(

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



        var rows = (await dbConnection.QueryAsync<TerminalSalesSummarySpRow>(

            new CommandDefinition(

                "spEventTransSummary1_New",

                parameters,

                commandType: CommandType.StoredProcedure,

                cancellationToken: cancellationToken)))

            .Select(r => r.ToDto())

            .ToList();



        var totalCount = parameters.Get<int>("TotalCount");

        return (rows, totalCount);

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

            Data = []

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


