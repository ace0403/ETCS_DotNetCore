using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.History;

namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentPurchaseHistory;

public sealed class StudentPurchaseHistoryReportRepository : IStudentPurchaseHistoryReportRepository
{
    private const int PosCardPurchaseType = HistoryConstants.PosCardPurchase;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;

    public StudentPurchaseHistoryReportRepository(
        IDbConnectionFactory connectionFactory,
        IMealDbConnectionFactory mealDbConnectionFactory)
    {
        _connectionFactory = connectionFactory;
        _mealDbConnectionFactory = mealDbConnectionFactory;
    }

    public async Task<IReadOnlyList<StudentPurchaseHistoryReportRowDto>> GetRowsAsync(
        StudentPurchaseHistoryReportFilter filter,
        CancellationToken cancellationToken = default)
    {
        var rows = await LoadMergedRowsAsync(filter, cancellationToken);
        return rows;
    }

    public async Task<StudentPurchaseHistoryReportPagedResult> GetRowsPagedAsync(
        StudentPurchaseHistoryReportListRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.StartDate is null || request.EndDate is null || request.SchoolId <= 0)
        {
            return EmptyPagedResult(request.Draw);
        }

        var filter = new StudentPurchaseHistoryReportFilter
        {
            StartDate = request.StartDate.Value.Date,
            EndDate = request.EndDate.Value.Date,
            SchoolId = request.SchoolId,
            StudentUserId = request.StudentUserId
        };

        var allRows = await LoadMergedRowsAsync(filter, cancellationToken);
        var pageSize = request.PageSize;
        var start = Math.Max(0, request.Start);
        var page = allRows.Skip(start).Take(pageSize).ToList();

        return new StudentPurchaseHistoryReportPagedResult
        {
            Draw = request.Draw,
            RecordsTotal = allRows.Count,
            RecordsFiltered = allRows.Count,
            Data = page,
            Success = true
        };
    }

    private async Task<IReadOnlyList<StudentPurchaseHistoryReportRowDto>> LoadMergedRowsAsync(
        StudentPurchaseHistoryReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.SchoolId <= 0)
        {
            return [];
        }

        var students = await LoadSchoolStudentsAsync(filter, cancellationToken);
        if (students.Count == 0)
        {
            return [];
        }

        var posRows = await LoadPosRowsAsync(filter, students, cancellationToken);
        var mealRows = await LoadMealRowsAsync(filter, students, cancellationToken);

        return posRows
            .Concat(mealRows)
            .OrderBy(r => r.SortDateTime)
            .ThenBy(r => r.StudentName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.ItemName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<StudentRow>> LoadSchoolStudentsAsync(
        StudentPurchaseHistoryReportFilter filter,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                sl.UserId AS UserId,
                LTRIM(RTRIM(
                    ISNULL(sl.StudFirstName, '') + ' ' + ISNULL(sl.StudLastName, '')
                )) AS StudentName,
                LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudentCode,
                LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS Grade,
                LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) AS CustomerId
            FROM StudentLogin sl
            WHERE sl.StudSchoolId = @SchoolId
              AND (@StudentUserId <= 0 OR sl.UserId = @StudentUserId);
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<StudentRow>(new CommandDefinition(
            sql,
            new { filter.SchoolId, filter.StudentUserId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows
            .Where(s => !string.IsNullOrWhiteSpace(s.CustomerId) || s.UserId > 0)
            .ToList();
    }

    private async Task<IReadOnlyList<StudentPurchaseHistoryReportRowDto>> LoadPosRowsAsync(
        StudentPurchaseHistoryReportFilter filter,
        IReadOnlyList<StudentRow> students,
        CancellationToken cancellationToken)
    {
        var customerKeys = students
            .Select(s => s.CustomerId?.Trim() ?? string.Empty)
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (customerKeys.Length == 0)
        {
            return [];
        }

        var rangeStart = filter.StartDate.Date;
        var rangeEndExclusive = filter.EndDate.Date.AddDays(1);
        var studentByCustomer = students
            .Where(s => !string.IsNullOrWhiteSpace(s.CustomerId))
            .GroupBy(s => s.CustomerId.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        const string sql = """
            SELECT
                CustomerId = LTRIM(RTRIM(ISNULL(a.CustomerID, ''))),
                EventTime = a.LogDateTimeTerminal,
                Reference = LTRIM(RTRIM(ISNULL(a.TransactionID, ''))),
                SkuCode = LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                Amount = ISNULL(TRY_CAST(p.Amount AS decimal(18, 2)), 0)
            FROM dbo.AccessLog a
            INNER JOIN dbo.POSPurchase p
                ON LTRIM(RTRIM(ISNULL(p.TransId, ''))) = LTRIM(RTRIM(ISNULL(a.TransactionID, '')))
               AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            WHERE a.TransactionType = @TransactionType
              AND a.LogDateTimeTerminal >= @RangeStart
              AND a.LogDateTimeTerminal < @RangeEndExclusive
              AND LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) IN @CustomerKeys
            ORDER BY a.LogDateTimeTerminal ASC, p.Id ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var purchaseRows = (await connection.QueryAsync<PosPurchaseRow>(new CommandDefinition(
            sql,
            new
            {
                TransactionType = PosCardPurchaseType,
                RangeStart = rangeStart,
                RangeEndExclusive = rangeEndExclusive,
                CustomerKeys = customerKeys
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        if (purchaseRows.Count == 0)
        {
            return [];
        }

        var needsMealItemLookup = rangeEndExclusive > HistoryConstants.LegacyCutoffDate;
        var needsSkuLookup = rangeStart < HistoryConstants.LegacyCutoffDate;
        Dictionary<int, string> mealItemNames = [];
        Dictionary<string, string> skuNames = new(StringComparer.OrdinalIgnoreCase);

        if (needsMealItemLookup)
        {
            var mealItemIds = purchaseRows
                .Where(r => r.EventTime.Date >= HistoryConstants.LegacyCutoffDate)
                .Select(r => int.TryParse(r.SkuCode, out var id) ? id : 0)
                .Where(id => id > 0)
                .Distinct()
                .ToList();
            mealItemNames = await LoadMealItemNamesAsync(mealItemIds, cancellationToken);
        }

        if (needsSkuLookup)
        {
            var skuCodes = purchaseRows
                .Where(r => r.EventTime.Date < HistoryConstants.LegacyCutoffDate)
                .Select(r => r.SkuCode?.Trim() ?? string.Empty)
                .Where(c => c.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            skuNames = await LoadSkuNamesAsync(skuCodes, cancellationToken);
        }

        var result = new List<StudentPurchaseHistoryReportRowDto>(purchaseRows.Count);
        foreach (var row in purchaseRows)
        {
            if (!studentByCustomer.TryGetValue(row.CustomerId?.Trim() ?? string.Empty, out var student))
            {
                continue;
            }

            var skuCode = row.SkuCode?.Trim() ?? string.Empty;
            var useMealItemLookup = row.EventTime.Date >= HistoryConstants.LegacyCutoffDate;
            string itemName;
            if (useMealItemLookup
                && int.TryParse(skuCode, out var mealItemId)
                && mealItemNames.TryGetValue(mealItemId, out var mealName)
                && !string.IsNullOrWhiteSpace(mealName))
            {
                itemName = mealName;
            }
            else if (skuNames.TryGetValue(skuCode, out var skuName) && !string.IsNullOrWhiteSpace(skuName))
            {
                itemName = skuName;
            }
            else
            {
                itemName = string.IsNullOrWhiteSpace(skuCode) ? "(Unknown item)" : skuCode;
            }

            result.Add(new StudentPurchaseHistoryReportRowDto
            {
                StudentName = student.StudentName?.Trim() ?? string.Empty,
                StudentCode = student.StudentCode?.Trim() ?? string.Empty,
                Grade = student.Grade?.Trim() ?? string.Empty,
                Source = "POS",
                ItemName = itemName,
                Quantity = 1,
                Amount = row.Amount,
                PurchaseDateTime = row.EventTime.ToString("dd/MM/yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                Reference = row.Reference?.Trim() ?? string.Empty,
                SortDateTime = row.EventTime
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<StudentPurchaseHistoryReportRowDto>> LoadMealRowsAsync(
        StudentPurchaseHistoryReportFilter filter,
        IReadOnlyList<StudentRow> students,
        CancellationToken cancellationToken)
    {
        var studentIds = students.Select(s => s.UserId).Where(id => id > 0).Distinct().ToArray();
        if (studentIds.Length == 0)
        {
            return [];
        }

        var studentById = students
            .Where(s => s.UserId > 0)
            .GroupBy(s => s.UserId)
            .ToDictionary(g => g.Key, g => g.First());

        var rangeStart = filter.StartDate.Date;
        var rangeEndExclusive = filter.EndDate.Date.AddDays(1);

        const string sql = """
            SELECT
                o.StudentId,
                ItemName = LTRIM(RTRIM(ISNULL(
                    CASE
                        WHEN oi.PackageId IS NOT NULL THEN COALESCE(pkg_items.ItemsName, mp.PackageName)
                        ELSE mi.ItemName
                    END, ''))),
                Quantity = ISNULL(oi.Quantity, 1),
                Amount = ISNULL(
                    NULLIF(oi.Total, 0),
                    ISNULL(oi.ItemPrice, 0) * ISNULL(oi.Quantity, 1)),
                OrderId = o.Id,
                EventTime = oi.MealDate
            FROM [Order] o
            INNER JOIN [OrderItem] oi ON oi.OrderId = o.Id
            LEFT JOIN [MealPackages] mp ON mp.Id = oi.PackageId
            LEFT JOIN [MealItem] mi ON mi.Id = oi.ItemId
            OUTER APPLY (
                SELECT STRING_AGG(LTRIM(RTRIM(mi2.ItemName)), ', ') AS ItemsName
                FROM MealPackageItems mpi
                INNER JOIN MealItem mi2 ON mpi.MealItemId = mi2.Id
                WHERE mpi.MealPackageId = oi.PackageId
            ) pkg_items
            WHERE ISNULL(o.IsPaid, 0) = 1
              AND o.OrderTypeId IN (24, 42)
              AND oi.MealDate >= @RangeStart
              AND oi.MealDate < @RangeEndExclusive
              AND o.StudentId IN @StudentIds
            ORDER BY oi.MealDate ASC, o.Id ASC, oi.Id ASC;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var mealRows = (await dbConnection.QueryAsync<MealPurchaseRow>(new CommandDefinition(
            sql,
            new
            {
                RangeStart = rangeStart,
                RangeEndExclusive = rangeEndExclusive,
                StudentIds = studentIds
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        var result = new List<StudentPurchaseHistoryReportRowDto>(mealRows.Count);
        foreach (var row in mealRows)
        {
            if (!studentById.TryGetValue(row.StudentId, out var student))
            {
                continue;
            }

            var itemName = string.IsNullOrWhiteSpace(row.ItemName) ? "(Unknown item)" : row.ItemName.Trim();

            result.Add(new StudentPurchaseHistoryReportRowDto
            {
                StudentName = student.StudentName?.Trim() ?? string.Empty,
                StudentCode = student.StudentCode?.Trim() ?? string.Empty,
                Grade = student.Grade?.Trim() ?? string.Empty,
                Source = "Meal Order",
                ItemName = itemName,
                Quantity = row.Quantity <= 0 ? 1 : row.Quantity,
                Amount = row.Amount,
                PurchaseDateTime = row.EventTime.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
                Reference = row.OrderId.ToString(CultureInfo.InvariantCulture),
                SortDateTime = row.EventTime.Date
            });
        }

        return result;
    }

    private async Task<Dictionary<int, string>> LoadMealItemNamesAsync(
        IReadOnlyList<int> mealItemIds,
        CancellationToken cancellationToken)
    {
        if (mealItemIds.Count == 0)
        {
            return [];
        }

        const string sql = """
            SELECT
                Id,
                ItemName = LTRIM(RTRIM(ISNULL(ItemName, '')))
            FROM [MealItem]
            WHERE Id IN @MealItemIds;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<(int Id, string ItemName)>(new CommandDefinition(
            sql,
            new { MealItemIds = mealItemIds },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows.ToDictionary(row => row.Id, row => row.ItemName?.Trim() ?? string.Empty);
    }

    private async Task<Dictionary<string, string>> LoadSkuNamesAsync(
        IReadOnlyList<string> skuCodes,
        CancellationToken cancellationToken)
    {
        if (skuCodes.Count == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        const string sql = """
            SELECT
                ItemCode = LTRIM(RTRIM(ISNULL(ItemCode, ''))),
                ItemName = LTRIM(RTRIM(ISNULL(ItemName, '')))
            FROM dbo.SKU
            WHERE LTRIM(RTRIM(ISNULL(ItemCode, ''))) IN @SkuCodes;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<(string ItemCode, string ItemName)>(new CommandDefinition(
            sql,
            new { SkuCodes = skuCodes },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ItemCode))
            .GroupBy(r => r.ItemCode.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().ItemName?.Trim() ?? string.Empty, StringComparer.OrdinalIgnoreCase);
    }

    private static StudentPurchaseHistoryReportPagedResult EmptyPagedResult(int draw) =>
        new()
        {
            Draw = draw,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = [],
            Success = true
        };

    private sealed class StudentRow
    {
        public int UserId { get; init; }
        public string StudentName { get; init; } = string.Empty;
        public string StudentCode { get; init; } = string.Empty;
        public string Grade { get; init; } = string.Empty;
        public string CustomerId { get; init; } = string.Empty;
    }

    private sealed class PosPurchaseRow
    {
        public string CustomerId { get; init; } = string.Empty;
        public DateTime EventTime { get; init; }
        public string Reference { get; init; } = string.Empty;
        public string SkuCode { get; init; } = string.Empty;
        public decimal Amount { get; init; }
    }

    private sealed class MealPurchaseRow
    {
        public int StudentId { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public int Quantity { get; init; }
        public decimal Amount { get; init; }
        public long OrderId { get; init; }
        public DateTime EventTime { get; init; }
    }
}
