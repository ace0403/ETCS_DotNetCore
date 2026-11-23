using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.History;

namespace ETCS.Shared.Infrastructure.Admin.Reports.PosReceiptReprint;

public sealed class PosReceiptReprintRepository : IPosReceiptReprintRepository
{
    private static readonly int[] SearchTypes =
    [
        HistoryConstants.PosCashPurchase,
        HistoryConstants.PosCreditCardPurchase,
        HistoryConstants.PosCardPurchase,
        HistoryConstants.PosUndoPurchase
    ];

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;

    public PosReceiptReprintRepository(
        IDbConnectionFactory connectionFactory,
        IMealDbConnectionFactory mealDbConnectionFactory)
    {
        _connectionFactory = connectionFactory;
        _mealDbConnectionFactory = mealDbConnectionFactory;
    }

    public async Task<PosReceiptReprintPagedResult> SearchPagedAsync(
        PosReceiptReprintSearchFilter filter,
        int draw,
        CancellationToken cancellationToken = default)
    {
        var from = filter.From.Date;
        var toExclusive = filter.To.Date.AddDays(1);
        var transactionId = filter.TransactionId?.Trim() ?? string.Empty;
        var customerId = filter.CustomerId?.Trim() ?? string.Empty;
        var terminalCode = filter.TerminalCode?.Trim() ?? string.Empty;
        var schoolCodes = ResolveSchoolCodes(filter);
        var schoolCodesParam = schoolCodes.Count > 0 ? schoolCodes : ["__none__"];
        var pageSize = filter.PageSize;
        var start = Math.Max(0, filter.Start);

        const string whereSql = """
            WHERE a.LogDateTimeTerminal >= @From
              AND a.LogDateTimeTerminal < @ToExclusive
              AND a.TransactionType IN @TransactionTypes
              AND (@TransactionId = '' OR LTRIM(RTRIM(ISNULL(a.TransactionID, ''))) = @TransactionId)
              AND (@CustomerId = '' OR LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) = @CustomerId)
              AND (@TerminalCode = '' OR LTRIM(RTRIM(CAST(a.TerminalCode AS varchar(50)))) = @TerminalCode)
              AND (
                    (@SchoolCodesCount = 0 AND (@SchoolCode = '' OR LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50)))) = @SchoolCode))
                 OR (@SchoolCodesCount > 0 AND LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50)))) IN @SchoolCodes)
                  )
            """;

        var countSql = $"""
            SELECT COUNT(1)
            FROM dbo.AccessLog a
            {whereSql};
            """;

        var dataSql = $"""
            SELECT
                AccessLogId = a.RcdID,
                SaleDateTime = a.LogDateTimeTerminal,
                TransactionId = LTRIM(RTRIM(ISNULL(a.TransactionID, ''))),
                CustomerId = LTRIM(RTRIM(ISNULL(a.CustomerID, ''))),
                StudentName = CASE
                    WHEN LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) IN ('208', '209') THEN ''
                    ELSE LTRIM(RTRIM(
                        LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
                    ))
                END,
                TransactionType = a.TransactionType,
                Amount = ISNULL(a.Amount, 0),
                TerminalCode = LTRIM(RTRIM(ISNULL(CAST(a.TerminalCode AS varchar(50)), ''))),
                TerminalName = LTRIM(RTRIM(ISNULL(t.Description, ''))),
                BranchCode = LTRIM(RTRIM(ISNULL(CAST(a.BranchCode AS varchar(50)), ''))),
                SchoolName = LTRIM(RTRIM(ISNULL(si.SchoolName, ''))),
                HasLineItems = CAST(CASE WHEN EXISTS (
                    SELECT 1
                    FROM dbo.POSPurchase p
                    WHERE LTRIM(RTRIM(ISNULL(p.TransId, ''))) = LTRIM(RTRIM(ISNULL(a.TransactionID, '')))
                      AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
                ) THEN 1 ELSE 0 END AS bit)
            FROM dbo.AccessLog a
            LEFT JOIN dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            LEFT JOIN dbo.SchoolInfo si
                ON LTRIM(RTRIM(CAST(si.Schoolcode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50))))
            OUTER APPLY (
                SELECT TOP (1) Description
                FROM dbo.IDTerminals t
                WHERE LTRIM(RTRIM(CAST(t.TerminalCode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.TerminalCode AS varchar(50))))
                  AND LTRIM(RTRIM(CAST(t.branchcode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50))))
            ) t
            {whereSql}
            ORDER BY a.LogDateTimeTerminal DESC, a.RcdID DESC
            OFFSET @Start ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        var args = new
        {
            From = from,
            ToExclusive = toExclusive,
            TransactionTypes = SearchTypes,
            TransactionId = transactionId,
            CustomerId = customerId,
            TerminalCode = terminalCode,
            SchoolCode = filter.SchoolCode?.Trim() ?? string.Empty,
            SchoolCodes = schoolCodesParam,
            SchoolCodesCount = schoolCodes.Count,
            Start = start,
            PageSize = pageSize
        };

        using var connection = _connectionFactory.CreateConnection();
        var total = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            countSql,
            args,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        var rows = total == 0
            ? []
            : (await connection.QueryAsync<SearchRow>(new CommandDefinition(
                dataSql,
                args,
                commandType: CommandType.Text,
                cancellationToken: cancellationToken))).ToList();

        return new PosReceiptReprintPagedResult
        {
            Draw = draw,
            RecordsTotal = total,
            RecordsFiltered = total,
            Data = rows.Select(MapSearchRow).ToList(),
            Success = true
        };
    }

    public async Task<PosReceiptReprintDetailDto?> GetDetailAsync(
        string transactionId,
        string customerId,
        IReadOnlyList<string> allowedSchoolCodes,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var schoolCodes = (allowedSchoolCodes ?? [])
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        const string headerSql = """
            SELECT TOP (1)
                AccessLogId = a.RcdID,
                SaleDateTime = a.LogDateTimeTerminal,
                TransactionId = LTRIM(RTRIM(ISNULL(a.TransactionID, ''))),
                CustomerId = LTRIM(RTRIM(ISNULL(a.CustomerID, ''))),
                StudentName = CASE
                    WHEN LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) IN ('208', '209') THEN ''
                    ELSE LTRIM(RTRIM(
                        LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
                    ))
                END,
                TransactionType = a.TransactionType,
                Amount = ISNULL(a.Amount, 0),
                TerminalCode = LTRIM(RTRIM(ISNULL(CAST(a.TerminalCode AS varchar(50)), ''))),
                TerminalName = LTRIM(RTRIM(ISNULL(t.Description, ''))),
                BranchCode = LTRIM(RTRIM(ISNULL(CAST(a.BranchCode AS varchar(50)), ''))),
                SchoolName = LTRIM(RTRIM(ISNULL(si.SchoolName, '')))
            FROM dbo.AccessLog a
            LEFT JOIN dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            LEFT JOIN dbo.SchoolInfo si
                ON LTRIM(RTRIM(CAST(si.Schoolcode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50))))
            OUTER APPLY (
                SELECT TOP (1) Description
                FROM dbo.IDTerminals t
                WHERE LTRIM(RTRIM(CAST(t.TerminalCode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.TerminalCode AS varchar(50))))
                  AND LTRIM(RTRIM(CAST(t.branchcode AS varchar(50)))) = LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50))))
            ) t
            WHERE LTRIM(RTRIM(ISNULL(a.TransactionID, ''))) = @TransactionId
              AND LTRIM(RTRIM(ISNULL(a.CustomerID, ''))) = @CustomerId
              AND a.TransactionType IN @TransactionTypes
              AND (
                    @SchoolCodesCount = 0
                 OR LTRIM(RTRIM(CAST(a.BranchCode AS varchar(50)))) IN @SchoolCodes
                  )
            ORDER BY a.LogDateTimeTerminal DESC, a.RcdID DESC;
            """;

        var schoolCodesParam = schoolCodes.Count > 0 ? schoolCodes : ["__none__"];

        using var connection = _connectionFactory.CreateConnection();
        var header = await connection.QueryFirstOrDefaultAsync<SearchRow>(new CommandDefinition(
            headerSql,
            new
            {
                TransactionId = transactionId.Trim(),
                CustomerId = customerId.Trim(),
                TransactionTypes = SearchTypes,
                SchoolCodes = schoolCodesParam,
                SchoolCodesCount = schoolCodes.Count
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (header is null)
        {
            return null;
        }

        var lines = await LoadLinesAsync(header.TransactionId, header.CustomerId, header.SaleDateTime, cancellationToken);
        var hasLines = lines.Count > 0;
        var isUndo = header.TransactionType == HistoryConstants.PosUndoPurchase;

        var notice = string.Empty;
        if (!hasLines && !isUndo)
        {
            notice = "Item lines were not stored for this sale. Showing header total only.";
            lines =
            [
                new PosReceiptReprintLineDto
                {
                    SkuCode = string.Empty,
                    ItemName = "Sale total",
                    Amount = header.Amount,
                    Quantity = 1
                }
            ];
        }
        else if (isUndo && !hasLines)
        {
            notice = "Undo transactions do not retain original item lines.";
            lines =
            [
                new PosReceiptReprintLineDto
                {
                    SkuCode = string.Empty,
                    ItemName = "Undo amount",
                    Amount = header.Amount,
                    Quantity = 1
                }
            ];
        }

        return new PosReceiptReprintDetailDto
        {
            AccessLogId = header.AccessLogId,
            SaleDateTime = header.SaleDateTime,
            TransactionId = header.TransactionId,
            CustomerId = header.CustomerId,
            StudentName = header.StudentName?.Trim() ?? string.Empty,
            TransactionType = header.TransactionType,
            PaymentMode = HistoryConstants.ResolvePosPaymentModeLabel(header.TransactionType),
            TypeLabel = HistoryConstants.ResolvePosTypeLabel(header.TransactionType),
            Amount = header.Amount,
            TerminalCode = header.TerminalCode?.Trim() ?? string.Empty,
            TerminalName = header.TerminalName?.Trim() ?? string.Empty,
            BranchCode = header.BranchCode?.Trim() ?? string.Empty,
            SchoolName = header.SchoolName?.Trim() ?? string.Empty,
            IsUndo = isUndo,
            HasLineItems = hasLines,
            Lines = lines,
            LinesMissingNotice = notice
        };
    }

    private async Task<IReadOnlyList<PosReceiptReprintLineDto>> LoadLinesAsync(
        string transactionId,
        string customerId,
        DateTime saleDateTime,
        CancellationToken cancellationToken)
    {
        var useMealItemLookup = saleDateTime.Date >= HistoryConstants.LegacyCutoffDate;

        if (!useMealItemLookup)
        {
            const string legacySql = """
                SELECT
                    SkuCode = LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                    ItemName = COALESCE(
                        NULLIF(LTRIM(RTRIM(ISNULL(s.ItemName, ''))), ''),
                        LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                        ''),
                    Amount = ISNULL(TRY_CAST(p.Amount AS decimal(18, 2)), 0)
                FROM dbo.POSPurchase p
                LEFT JOIN dbo.SKU s
                    ON LTRIM(RTRIM(ISNULL(s.ItemCode, ''))) = LTRIM(RTRIM(ISNULL(p.SkuCode, '')))
                WHERE LTRIM(RTRIM(ISNULL(p.TransId, ''))) = @TransactionId
                  AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = @CustomerId
                ORDER BY p.Id ASC;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var legacyLines = await connection.QueryAsync<LineRow>(new CommandDefinition(
                legacySql,
                new { TransactionId = transactionId, CustomerId = customerId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

            return legacyLines
                .Select(row => new PosReceiptReprintLineDto
                {
                    SkuCode = row.SkuCode?.Trim() ?? string.Empty,
                    ItemName = string.IsNullOrWhiteSpace(row.ItemName) ? (row.SkuCode?.Trim() ?? string.Empty) : row.ItemName.Trim(),
                    Amount = row.Amount,
                    Quantity = 1
                })
                .ToList();
        }

        const string purchaseSql = """
            SELECT
                SkuCode = LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                Amount = ISNULL(TRY_CAST(p.Amount AS decimal(18, 2)), 0)
            FROM dbo.POSPurchase p
            WHERE LTRIM(RTRIM(ISNULL(p.TransId, ''))) = @TransactionId
              AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = @CustomerId
            ORDER BY p.Id ASC;
            """;

        using var ibonusConnection = _connectionFactory.CreateConnection();
        var purchaseRows = (await ibonusConnection.QueryAsync<LineRow>(new CommandDefinition(
            purchaseSql,
            new { TransactionId = transactionId, CustomerId = customerId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        if (purchaseRows.Count == 0)
        {
            return [];
        }

        var mealItemIds = purchaseRows
            .Select(row => int.TryParse(row.SkuCode, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var mealItemNames = await LoadMealItemNamesAsync(mealItemIds, cancellationToken);

        return purchaseRows
            .Select(row =>
            {
                var sku = row.SkuCode?.Trim() ?? string.Empty;
                var name = int.TryParse(sku, out var mealItemId)
                    && mealItemNames.TryGetValue(mealItemId, out var itemName)
                    && !string.IsNullOrWhiteSpace(itemName)
                        ? itemName
                        : sku;

                return new PosReceiptReprintLineDto
                {
                    SkuCode = sku,
                    ItemName = name,
                    Amount = row.Amount,
                    Quantity = 1
                };
            })
            .ToList();
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

    private static IReadOnlyList<string> ResolveSchoolCodes(PosReceiptReprintSearchFilter filter)
    {
        if (filter.AllowedSchoolCodes is { Count: > 0 })
        {
            return filter.AllowedSchoolCodes
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(filter.SchoolCode))
        {
            return [filter.SchoolCode.Trim()];
        }

        return [];
    }

    private static PosReceiptReprintSearchRowDto MapSearchRow(SearchRow row) =>
        new()
        {
            AccessLogId = row.AccessLogId,
            SaleDateTime = row.SaleDateTime,
            TransactionId = row.TransactionId?.Trim() ?? string.Empty,
            CustomerId = row.CustomerId?.Trim() ?? string.Empty,
            StudentName = row.StudentName?.Trim() ?? string.Empty,
            TransactionType = row.TransactionType,
            PaymentMode = HistoryConstants.ResolvePosPaymentModeLabel(row.TransactionType),
            TypeLabel = HistoryConstants.ResolvePosTypeLabel(row.TransactionType),
            Amount = row.Amount,
            TerminalCode = row.TerminalCode?.Trim() ?? string.Empty,
            TerminalName = row.TerminalName?.Trim() ?? string.Empty,
            BranchCode = row.BranchCode?.Trim() ?? string.Empty,
            SchoolName = row.SchoolName?.Trim() ?? string.Empty,
            HasLineItems = row.HasLineItems
        };

    private sealed class SearchRow
    {
        public long AccessLogId { get; init; }

        public DateTime SaleDateTime { get; init; }

        public string TransactionId { get; init; } = string.Empty;

        public string CustomerId { get; init; } = string.Empty;

        public string StudentName { get; init; } = string.Empty;

        public int TransactionType { get; init; }

        public decimal Amount { get; init; }

        public string TerminalCode { get; init; } = string.Empty;

        public string TerminalName { get; init; } = string.Empty;

        public string BranchCode { get; init; } = string.Empty;

        public string SchoolName { get; init; } = string.Empty;

        public bool HasLineItems { get; init; }
    }

    private sealed class LineRow
    {
        public string SkuCode { get; init; } = string.Empty;

        public string ItemName { get; init; } = string.Empty;

        public decimal Amount { get; init; }
    }
}
