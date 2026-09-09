using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

public sealed class StudentConsumptionReportRepository : IStudentConsumptionReportRepository
{
    private const int ExportBatchSize = 25;
    private const int CustomerKeyBatchSize = 400;
    private const int DefaultStudentPageSize = 10;
    private const int MaxStudentPageSize = 50;
    private static readonly TimeSpan EligibleStudentsCacheDuration = TimeSpan.FromMinutes(3);

    private static readonly int[] ReportTransactionTypes =
    [
        23, 1004, 10001, 21004, 1007, 21007, 21002, 21006
    ];

    private const string SchoolStudentsSql = """
        SELECT
            sl.UserId AS UserId,
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) AS StudFirstName,
            LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS StudLastName,
            LTRIM(RTRIM(
                ISNULL(sl.StudFirstName, '') + ' ' + ISNULL(sl.StudLastName, '')
            )) AS Name,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudCode,
            LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS Grade,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerID)), ''), sl.StudCode))) AS AccessCustomerKey,
            ISNULL(sl.StudSchoolId, 0) AS SchoolId
        FROM StudentLogin sl
        WHERE (
                @StudentUserId <= 0
                OR sl.UserId = @StudentUserId
              )
          AND (
                @ApplySchoolScope = 0
                OR sl.StudSchoolId IN @ScopedSchoolIds
              );
        """;

    private const string CustomerAccessStatsSql = """
        SELECT
            LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) AS CustomerKey,
            ISNULL(SUM(
                CASE
                    WHEN a.LogDateTimeServer < @PeriodStart THEN
                        CASE
                            WHEN a.TransactionType IN (23, 1004, 10001, 21004) THEN ABS(ISNULL(a.Amount, 0))
                            WHEN a.TransactionType IN (1007, 21007) THEN -ABS(ISNULL(a.Amount, 0))
                            WHEN a.TransactionType = 21002 THEN -ABS(ISNULL(a.Amount, 0))
                            WHEN a.TransactionType = 21006 THEN ABS(ISNULL(a.Amount, 0))
                            ELSE 0
                        END
                    ELSE 0
                END
            ), 0) AS OpeningBalance,
            MAX(CASE
                    WHEN a.LogDateTimeServer >= @PeriodStart
                     AND a.LogDateTimeServer < @PeriodEndExclusive THEN 1
                    ELSE 0
                END) AS HasPeriodActivity
        FROM AccessLog a
        WHERE a.CustomerID IN @CustomerKeys
          AND a.TransactionType IN @TransactionTypes
          AND a.LogDateTimeServer < @PeriodEndExclusive
        GROUP BY LTRIM(RTRIM(ISNULL(a.CustomerID, '')));
        """;

    private const string PeriodTransactionsByCustomerSql = """
        SELECT
            LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) AS CustomerKey,
            a.LogDateTimeServer AS TransDateTime,
            a.TransactionType,
            ABS(ISNULL(a.Amount, 0)) AS Amount
        FROM AccessLog a
        WHERE a.CustomerID IN @CustomerKeys
          AND a.LogDateTimeServer >= @PeriodStart
          AND a.LogDateTimeServer < @PeriodEndExclusive
          AND a.TransactionType IN @TransactionTypes
        ORDER BY LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) ASC, a.LogDateTimeServer ASC, a.TransactionID ASC;
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
    private readonly IMemoryCache _cache;

    public StudentConsumptionReportRepository(
        IDbConnectionFactory connectionFactory,
        IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    public async Task<StudentConsumptionReportPagedData> GetReportPagedAsync(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        int studentSkip,
        int studentTake,
        CancellationToken cancellationToken = default)
    {
        var periodStart = filter.StartDate.Date;
        var periodEndExclusive = filter.EndDate.Date.AddDays(1);
        if (periodStart >= periodEndExclusive)
        {
            return new StudentConsumptionReportPagedData();
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var eligibleStudents = await GetEligibleStudentsAsync(
            dbConnection,
            filter,
            scopedSchoolIds,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        if (eligibleStudents.Count == 0)
        {
            return new StudentConsumptionReportPagedData();
        }

        studentTake = filter.StudentUserId > 0
            ? eligibleStudents.Count
            : NormalizeStudentPageSize(studentTake);
        studentSkip = filter.StudentUserId > 0 ? 0 : Math.Max(0, studentSkip);

        if (studentSkip >= eligibleStudents.Count)
        {
            return new StudentConsumptionReportPagedData { TotalStudents = eligibleStudents.Count };
        }

        var pageStudents = eligibleStudents
            .Skip(studentSkip)
            .Take(studentTake)
            .ToList();

        var rows = await BuildRowsForSchoolStudentsAsync(
            dbConnection,
            pageStudents,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        return new StudentConsumptionReportPagedData
        {
            TotalStudents = eligibleStudents.Count,
            Rows = rows
        };
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

        var eligibleStudents = await GetEligibleStudentsAsync(
            dbConnection,
            filter,
            scopedSchoolIds,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        if (filter.StudentUserId > 0 && eligibleStudents.Count == 0)
        {
            return new StudentConsumptionReportResult();
        }

        var allRows = new List<StudentConsumptionReportRowDto>();
        foreach (var batch in eligibleStudents.Chunk(ExportBatchSize))
        {
            var batchRows = await BuildRowsForSchoolStudentsAsync(
                dbConnection,
                batch,
                periodStart,
                periodEndExclusive,
                cancellationToken);
            allRows.AddRange(batchRows);
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

    private async Task<List<SchoolStudentRow>> GetEligibleStudentsAsync(
        DbConnection dbConnection,
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildEligibleStudentsCacheKey(filter, scopedSchoolIds, periodStart, periodEndExclusive);
        if (_cache.TryGetValue(cacheKey, out List<SchoolStudentRow>? cached) && cached is not null)
        {
            return cached;
        }

        var queryContext = CreateQueryContext(filter, scopedSchoolIds, periodEndExclusive);
        var schoolStudents = await LoadSchoolStudentsAsync(dbConnection, queryContext, cancellationToken);
        if (schoolStudents.Count == 0)
        {
            return [];
        }

        var customerKeys = schoolStudents
            .Select(s => s.AccessCustomerKey)
            .Where(key => !string.IsNullOrWhiteSpace(key))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (customerKeys.Count == 0)
        {
            return [];
        }

        var stats = await LoadCustomerAccessStatsAsync(
            dbConnection,
            customerKeys,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        var eligibleStudents = schoolStudents
            .Where(student =>
            {
                if (!stats.TryGetValue(student.AccessCustomerKey, out var summary))
                {
                    return false;
                }

                return summary.HasPeriodActivity;
            })
            .ToList();

        _cache.Set(cacheKey, eligibleStudents, EligibleStudentsCacheDuration);
        return eligibleStudents;
    }

    private static string BuildEligibleStudentsCacheKey(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        DateTime periodStart,
        DateTime periodEndExclusive)
    {
        var scopeKey = scopedSchoolIds is { Count: > 0 }
            ? string.Join(',', scopedSchoolIds.OrderBy(id => id))
            : "all";

        return string.Join(
            ':',
            "student-consumption-eligible-v2",
            filter.SchoolId,
            periodStart.ToString("yyyyMMdd"),
            periodEndExclusive.ToString("yyyyMMdd"),
            filter.StudentUserId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            scopeKey);
    }

    private static StudentQueryContext CreateQueryContext(
        StudentConsumptionReportFilter filter,
        IReadOnlyList<int>? scopedSchoolIds,
        DateTime periodEndExclusive)
    {
        var applySchoolScope = scopedSchoolIds is { Count: > 0 };
        return new StudentQueryContext
        {
            StudentUserId = filter.StudentUserId,
            PeriodEndExclusive = periodEndExclusive,
            TransactionTypes = ReportTransactionTypes,
            ApplySchoolScope = applySchoolScope ? 1 : 0,
            ScopedSchoolIds = applySchoolScope ? scopedSchoolIds! : [0]
        };
    }

    private static int NormalizeStudentPageSize(int studentTake)
    {
        if (studentTake <= 0)
        {
            return DefaultStudentPageSize;
        }

        return Math.Min(studentTake, MaxStudentPageSize);
    }

    private static async Task<List<SchoolStudentRow>> LoadSchoolStudentsAsync(
        DbConnection dbConnection,
        StudentQueryContext queryContext,
        CancellationToken cancellationToken)
    {
        var rows = await dbConnection.QueryAsync<SchoolStudentRow>(
            new CommandDefinition(
                SchoolStudentsSql,
                queryContext,
                cancellationToken: cancellationToken));

        return rows
            .Where(s => !string.IsNullOrWhiteSpace(s.AccessCustomerKey))
            .OrderBy(s => s.StudFirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.StudLastName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(s => s.StudCode, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<Dictionary<string, CustomerAccessSummary>> LoadCustomerAccessStatsAsync(
        DbConnection dbConnection,
        IReadOnlyList<string> customerKeys,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var stats = new Dictionary<string, CustomerAccessSummary>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in customerKeys.Chunk(CustomerKeyBatchSize))
        {
            var rows = await dbConnection.QueryAsync<CustomerAccessSummaryRow>(
                new CommandDefinition(
                    CustomerAccessStatsSql,
                    new
                    {
                        CustomerKeys = batch,
                        PeriodStart = periodStart,
                        PeriodEndExclusive = periodEndExclusive,
                        TransactionTypes = ReportTransactionTypes
                    },
                    cancellationToken: cancellationToken));

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CustomerKey))
                {
                    continue;
                }

                stats[row.CustomerKey] = new CustomerAccessSummary
                {
                    OpeningBalance = row.OpeningBalance,
                    HasPeriodActivity = row.HasPeriodActivity == 1
                };
            }
        }

        return stats;
    }

    private static async Task<List<StudentConsumptionReportRowDto>> BuildRowsForSchoolStudentsAsync(
        DbConnection dbConnection,
        IReadOnlyList<SchoolStudentRow> students,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        if (students.Count == 0)
        {
            return [];
        }

        var customerKeys = students
            .Select(s => s.AccessCustomerKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var stats = await LoadCustomerAccessStatsAsync(
            dbConnection,
            customerKeys,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        var transactions = await LoadPeriodTransactionsByCustomerAsync(
            dbConnection,
            customerKeys,
            periodStart,
            periodEndExclusive,
            cancellationToken);

        var allRows = new List<StudentConsumptionReportRowDto>();
        foreach (var student in students)
        {
            stats.TryGetValue(student.AccessCustomerKey, out var summary);
            transactions.TryGetValue(student.AccessCustomerKey, out var studentTransactions);
            studentTransactions ??= [];

            if (studentTransactions.Count == 0)
            {
                continue;
            }

            var openingBalance = summary?.OpeningBalance ?? 0m;
            allRows.AddRange(BuildStudentBlock(student.ToHeader(), openingBalance, studentTransactions));
        }

        return allRows;
    }

    private static async Task<Dictionary<string, List<AccessLogTransactionRow>>> LoadPeriodTransactionsByCustomerAsync(
        DbConnection dbConnection,
        IReadOnlyList<string> customerKeys,
        DateTime periodStart,
        DateTime periodEndExclusive,
        CancellationToken cancellationToken)
    {
        var transactions = new Dictionary<string, List<AccessLogTransactionRow>>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in customerKeys.Chunk(CustomerKeyBatchSize))
        {
            var rows = await dbConnection.QueryAsync<AccessLogTransactionRow>(
                new CommandDefinition(
                    PeriodTransactionsByCustomerSql,
                    new
                    {
                        CustomerKeys = batch,
                        PeriodStart = periodStart,
                        PeriodEndExclusive = periodEndExclusive,
                        TransactionTypes = ReportTransactionTypes
                    },
                    cancellationToken: cancellationToken));

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.CustomerKey))
                {
                    continue;
                }

                if (!transactions.TryGetValue(row.CustomerKey, out var list))
                {
                    list = [];
                    transactions[row.CustomerKey] = list;
                }

                list.Add(row);
            }
        }

        return transactions;
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

    private sealed class SchoolStudentRow
    {
        public decimal UserId { get; init; }
        public string StudFirstName { get; init; } = string.Empty;
        public string StudLastName { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string StudCode { get; init; } = string.Empty;
        public string Grade { get; init; } = string.Empty;
        public string AccessCustomerKey { get; init; } = string.Empty;
        public int SchoolId { get; init; }

        public StudentConsumptionStudentHeaderDto ToHeader() =>
            new()
            {
                UserId = UserId,
                Name = Name,
                StudCode = StudCode,
                Grade = Grade,
                SchoolId = SchoolId
            };
    }

    private sealed class AccessLogTransactionRow
    {
        public string CustomerKey { get; init; } = string.Empty;
        public DateTime TransDateTime { get; init; }
        public int TransactionType { get; init; }
        public decimal Amount { get; init; }
    }

    private sealed class CustomerAccessSummaryRow
    {
        public string CustomerKey { get; init; } = string.Empty;
        public decimal OpeningBalance { get; init; }
        public int HasPeriodActivity { get; init; }
    }

    private sealed class CustomerAccessSummary
    {
        public decimal OpeningBalance { get; init; }
        public bool HasPeriodActivity { get; init; }
    }

    private sealed class StudentQueryContext
    {
        public decimal StudentUserId { get; init; }
        public DateTime PeriodEndExclusive { get; init; }
        public int[] TransactionTypes { get; init; } = [];
        public int ApplySchoolScope { get; init; }
        public IReadOnlyList<int> ScopedSchoolIds { get; init; } = [];
    }
}
