using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

public sealed class StudentConsumptionReportRepository : IStudentConsumptionReportRepository
{
    private static readonly int[] ReportTransactionTypes =
    [
        23, 1004, 10001, 21004, 1007, 21007, 21002, 21006
    ];

    private const string StudentHeaderSql = """
        SELECT TOP (1)
            sl.UserId AS UserId,
            LTRIM(RTRIM(
                ISNULL(sl.StudFirstName, '') + ' ' + ISNULL(sl.StudLastName, '')
            )) AS Name,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudCode,
            LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS Grade,
            ISNULL(sl.StudSchoolId, 0) AS SchoolId
        FROM StudentLogin sl
        WHERE sl.UserId = @StudentUserId;
        """;

    private const string StudentIdsSql = """
        SELECT s.UserId
        FROM (
            SELECT DISTINCT
                sl.UserId,
                sl.StudFirstName,
                sl.StudLastName,
                sl.StudCode
            FROM StudentLogin sl
            INNER JOIN AccessLog a
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            WHERE a.TransactionType IN @TransactionTypes
              AND a.LogDateTimeServer < @PeriodEndExclusive
              AND (
                    @StudentUserId <= 0
                    OR sl.UserId = @StudentUserId
                  )
              AND (
                    @ApplySchoolScope = 0
                    OR sl.StudSchoolId IN @ScopedSchoolIds
                  )
        ) s
        ORDER BY s.StudFirstName, s.StudLastName, s.StudCode;
        """;

    private const string OpeningBalanceSql = """
        SELECT ISNULL(SUM(
            CASE
                WHEN a.TransactionType IN (23, 1004, 10001, 21004) THEN ABS(ISNULL(a.Amount, 0))
                WHEN a.TransactionType IN (1007, 21007) THEN -ABS(ISNULL(a.Amount, 0))
                WHEN a.TransactionType = 21002 THEN -ABS(ISNULL(a.Amount, 0))
                WHEN a.TransactionType = 21006 THEN ABS(ISNULL(a.Amount, 0))
                ELSE 0
            END
        ), 0)
        FROM AccessLog a
        INNER JOIN StudentLogin sl
            ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
        WHERE sl.UserId = @StudentUserId
          AND a.LogDateTimeServer < @PeriodStart
          AND a.TransactionType IN @TransactionTypes;
        """;

    private const string PeriodTransactionsSql = """
        SELECT
            a.LogDateTimeServer AS TransDateTime,
            a.TransactionType,
            ABS(ISNULL(a.Amount, 0)) AS Amount
        FROM AccessLog a
        INNER JOIN StudentLogin sl
            ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
        WHERE sl.UserId = @StudentUserId
          AND a.LogDateTimeServer >= @PeriodStart
          AND a.LogDateTimeServer < @PeriodEndExclusive
          AND a.TransactionType IN @TransactionTypes
        ORDER BY a.LogDateTimeServer ASC, a.TransactionID ASC;
        """;

    private const string SearchStudentsSql = """
        SELECT TOP (@MaxResults)
            sl.UserId AS UserId,
            LTRIM(RTRIM(
                ISNULL(sl.StudFirstName, '') + ' ' + ISNULL(sl.StudLastName, '')
            )) AS Name,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudCode
        FROM StudentLogin sl
        WHERE (
                @Search = ''
                OR LTRIM(RTRIM(ISNULL(sl.StudCode, ''))) LIKE '%' + @Search + '%'
                OR LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) LIKE '%' + @Search + '%'
                OR LTRIM(RTRIM(ISNULL(sl.StudFirstName, '') + ' ' + ISNULL(sl.StudLastName, ''))) LIKE '%' + @Search + '%'
              )
          AND (
                @ApplySchoolScope = 0
                OR sl.StudSchoolId IN @ScopedSchoolIds
              )
        ORDER BY sl.StudFirstName, sl.StudLastName, sl.StudCode;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public StudentConsumptionReportRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<StudentConsumptionReportResult> GetReportAsync(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        CancellationToken cancellationToken = default)
    {
        var periodStart = filter.StartDate.Date;
        var periodEndExclusive = filter.EndDate.Date.AddDays(1);
        if (periodStart >= periodEndExclusive)
        {
            return new StudentConsumptionReportResult();
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var applySchoolScope = scopedSchoolIds is { Count: > 0 };
        var schoolIds = applySchoolScope ? scopedSchoolIds! : [0];

        var studentIds = (await dbConnection.QueryAsync<decimal>(
            new CommandDefinition(
                StudentIdsSql,
                new
                {
                    filter.StudentUserId,
                    PeriodEndExclusive = periodEndExclusive,
                    TransactionTypes = ReportTransactionTypes,
                    ApplySchoolScope = applySchoolScope ? 1 : 0,
                    ScopedSchoolIds = schoolIds
                },
                cancellationToken: cancellationToken))).ToList();

        if (filter.StudentUserId > 0 && studentIds.Count == 0)
        {
            return new StudentConsumptionReportResult();
        }

        var allRows = new List<StudentConsumptionReportRowDto>();

        foreach (var studentUserId in studentIds)
        {
            var header = await dbConnection.QuerySingleOrDefaultAsync<StudentConsumptionStudentHeaderDto>(
                new CommandDefinition(
                    StudentHeaderSql,
                    new { StudentUserId = studentUserId },
                    cancellationToken: cancellationToken));

            if (header is null)
            {
                continue;
            }

            var queryParams = new
            {
                StudentUserId = studentUserId,
                PeriodStart = periodStart,
                PeriodEndExclusive = periodEndExclusive,
                TransactionTypes = ReportTransactionTypes
            };

            var openingBalance = await dbConnection.ExecuteScalarAsync<decimal>(
                new CommandDefinition(
                    OpeningBalanceSql,
                    queryParams,
                    cancellationToken: cancellationToken));

            var transactions = (await dbConnection.QueryAsync<AccessLogTransactionRow>(
                new CommandDefinition(
                    PeriodTransactionsSql,
                    queryParams,
                    cancellationToken: cancellationToken))).ToList();

            if (openingBalance == 0m && transactions.Count == 0)
            {
                continue;
            }

            allRows.AddRange(BuildStudentBlock(header, openingBalance, transactions));
        }

        return new StudentConsumptionReportResult { Rows = allRows };
    }

    public async Task<IReadOnlyList<StudentConsumptionStudentLookupDto>> SearchStudentsAsync(
        string term,
        IReadOnlyList<int>? scopedSchoolIds,
        int maxResults = 20,
        CancellationToken cancellationToken = default)
    {
        var search = (term ?? string.Empty).Trim();
        if (search.Length == 0)
        {
            return [];
        }

        maxResults = maxResults <= 0 ? 20 : Math.Min(maxResults, 50);
        var applySchoolScope = scopedSchoolIds is { Count: > 0 };
        var schoolIds = applySchoolScope ? scopedSchoolIds! : [0];

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;

        try
        {
            await dbConnection.OpenAsync(cancellationToken);

            var rows = await dbConnection.QueryAsync<StudentConsumptionStudentLookupDto>(
                new CommandDefinition(
                    SearchStudentsSql,
                    new
                    {
                        Search = search,
                        MaxResults = maxResults,
                        ApplySchoolScope = applySchoolScope ? 1 : 0,
                        ScopedSchoolIds = schoolIds
                    },
                    cancellationToken: cancellationToken));

            return rows.ToList();
        }
        catch (OperationCanceledException)
        {
            return [];
        }
    }

    private static List<StudentConsumptionReportRowDto> BuildStudentBlock(
        StudentConsumptionStudentHeaderDto header,
        decimal openingBalance,
        IReadOnlyList<AccessLogTransactionRow> transactions)
    {
        var rows = new List<StudentConsumptionReportRowDto>();
        decimal totalDebit = 0m;
        decimal totalCredit = 0m;

        rows.Add(new StudentConsumptionReportRowDto
        {
            StudentUserId = header.UserId,
            StudentName = header.Name,
            StudCode = header.StudCode,
            CustomerDetails = header.Grade,
            TransDate = "Opening Balance",
            Debit = 0m,
            Credit = 0m,
            Amount = openingBalance,
            RowKind = StudentConsumptionRowKind.Opening
        });

        foreach (var txn in transactions)
        {
            var (debit, credit) = MapTransactionAmounts(txn.TransactionType, txn.Amount);
            totalDebit += debit;
            totalCredit += credit;

            rows.Add(new StudentConsumptionReportRowDto
            {
                StudentUserId = header.UserId,
                TransDate = txn.TransDateTime.ToString("dd-MMM-yyyy"),
                Debit = debit,
                Credit = credit,
                Amount = credit - debit,
                RowKind = StudentConsumptionRowKind.Transaction
            });
        }

        var unutilisedBalance = openingBalance + totalCredit - totalDebit;
        var totalLabel = string.IsNullOrWhiteSpace(header.Grade)
            ? "Total"
            : $"{header.Grade} Total";

        rows.Add(new StudentConsumptionReportRowDto
        {
            StudentUserId = header.UserId,
            CustomerDetails = totalLabel,
            Debit = totalDebit,
            Credit = totalCredit,
            Amount = unutilisedBalance,
            RowKind = StudentConsumptionRowKind.Total
        });

        return rows;
    }

    private static (decimal Debit, decimal Credit) MapTransactionAmounts(int transactionType, decimal amount)
    {
        return transactionType switch
        {
            21002 => (amount, 0m),
            21006 => (-amount, 0m),
            23 or 1004 or 10001 or 21004 => (0m, amount),
            1007 or 21007 => (0m, -amount),
            _ => (0m, 0m)
        };
    }

    private sealed class AccessLogTransactionRow
    {
        public DateTime TransDateTime { get; init; }
        public int TransactionType { get; init; }
        public decimal Amount { get; init; }
    }
}
