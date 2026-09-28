using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.History;

public sealed class HistoryDetailRepository : IHistoryDetailRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;

    public HistoryDetailRepository(
        IDbConnectionFactory connectionFactory,
        IMealDbConnectionFactory mealDbConnectionFactory)
    {
        _connectionFactory = connectionFactory;
        _mealDbConnectionFactory = mealDbConnectionFactory;
    }

    public async Task<AccessLogHeaderDto?> GetAccessLogHeaderAsync(
        int guardianId,
        long accessLogId,
        CancellationToken cancellationToken)
    {
        if (guardianId <= 0 || accessLogId <= 0)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                AccessLogId = a.RcdID,
                TransactionId = LTRIM(RTRIM(ISNULL(a.TransactionID, ''))),
                CustomerId = LTRIM(RTRIM(ISNULL(a.CustomerID, ''))),
                Amount = ISNULL(a.Amount, 0),
                LogDateTimeServer = a.LogDateTimeServer,
                TransactionType = a.TransactionType,
                Description = LTRIM(RTRIM(ISNULL(a.Description, ''))),
                GuardianId = sl.GrdId,
                StudentName = LTRIM(RTRIM(
                    LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
                ))
            FROM dbo.AccessLog a
            INNER JOIN dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            WHERE a.RcdID = @AccessLogId
              AND sl.GrdId = @GuardianId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<AccessLogHeaderDto>(new CommandDefinition(
            sql,
            new { AccessLogId = accessLogId, GuardianId = guardianId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<HistoryLineItemDto>> GetPosPurchaseDetailAsync(
        AccessLogHeaderDto header,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(header.TransactionId) || string.IsNullOrWhiteSpace(header.CustomerId))
        {
            return [];
        }

        var useMealItemLookup = header.LogDateTimeServer.Date >= HistoryConstants.LegacyCutoffDate;

        if (!useMealItemLookup)
        {
            const string legacySql = """
                SELECT
                    SkuCode = LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                    ItemName = COALESCE(
                        NULLIF(LTRIM(RTRIM(ISNULL(s.ItemName, ''))), ''),
                        LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                        ''),
                    Amount = ISNULL(TRY_CAST(p.Amount AS decimal(18, 2)), 0),
                    DeliveryDate = TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(p.Purdate, ''))), '') AS datetime)
                FROM dbo.POSPurchase p
                LEFT JOIN dbo.SKU s
                    ON LTRIM(RTRIM(ISNULL(s.ItemCode, ''))) = LTRIM(RTRIM(ISNULL(p.SkuCode, '')))
                WHERE LTRIM(RTRIM(ISNULL(p.TransId, ''))) = LTRIM(RTRIM(@TransactionId))
                  AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = LTRIM(RTRIM(@CustomerId))
                ORDER BY p.Id ASC;
                """;

            using var connection = _connectionFactory.CreateConnection();
            var lines = await connection.QueryAsync<HistoryLineItemDto>(new CommandDefinition(
                legacySql,
                new { header.TransactionId, header.CustomerId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

            return lines.ToList();
        }

        const string purchaseSql = """
            SELECT
                SkuCode = LTRIM(RTRIM(ISNULL(p.SkuCode, ''))),
                Amount = ISNULL(TRY_CAST(p.Amount AS decimal(18, 2)), 0),
                DeliveryDate = TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(p.Purdate, ''))), '') AS datetime)
            FROM dbo.POSPurchase p
            WHERE LTRIM(RTRIM(ISNULL(p.TransId, ''))) = LTRIM(RTRIM(@TransactionId))
              AND LTRIM(RTRIM(ISNULL(p.Customerid, ''))) = LTRIM(RTRIM(@CustomerId))
            ORDER BY p.Id ASC;
            """;

        using var ibonusConnection = _connectionFactory.CreateConnection();
        var purchaseRows = (await ibonusConnection.QueryAsync<PurchaseRow>(new CommandDefinition(
            purchaseSql,
            new { header.TransactionId, header.CustomerId },
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
                var skuCode = row.SkuCode?.Trim() ?? string.Empty;
                var itemName = int.TryParse(skuCode, out var mealItemId)
                    && mealItemNames.TryGetValue(mealItemId, out var name)
                    && !string.IsNullOrWhiteSpace(name)
                        ? name
                        : skuCode;

                return new HistoryLineItemDto
                {
                    SkuCode = skuCode,
                    ItemName = itemName,
                    Amount = row.Amount,
                    DeliveryDate = row.DeliveryDate
                };
            })
            .ToList();
    }

    public async Task<IReadOnlyList<HistoryLineItemDto>> GetLegacyMealOrderDetailAsync(
        AccessLogHeaderDto header,
        CancellationToken cancellationToken)
    {
        if (header.LogDateTimeServer.Date >= HistoryConstants.LegacyCutoffDate
            || header.TransactionType != HistoryConstants.AccessLogMealOrderType)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(header.TransactionId) || string.IsNullOrWhiteSpace(header.CustomerId))
        {
            return [];
        }

        const string linesSql = """
            SELECT
                SkuCode = LTRIM(RTRIM(ISNULL(w.ItemCode, ''))),
                ItemName = LTRIM(RTRIM(ISNULL(w.ItemName, ''))),
                Amount = ISNULL(TRY_CAST(w.Price AS decimal(18, 2)), 0),
                DeliveryDate = w.CreDate
            FROM dbo.WeeklyOrders w
            WHERE LTRIM(RTRIM(ISNULL(w.OrderId, ''))) = LTRIM(RTRIM(@TransactionId))
              AND LTRIM(RTRIM(ISNULL(w.Customerid, ''))) = LTRIM(RTRIM(@CustomerId))
            ORDER BY w.Id ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var lines = await connection.QueryAsync<HistoryLineItemDto>(new CommandDefinition(
            linesSql,
            new { header.TransactionId, header.CustomerId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return lines.ToList();
    }

    public async Task<int?> GetAccessLogTransactionTypeByGatewayTransactionAsync(
        int guardianId,
        string gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        if (guardianId <= 0 || string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1) a.TransactionType
            FROM dbo.AccessLog a
            INNER JOIN dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            WHERE sl.GrdId = @GuardianId
              AND LTRIM(RTRIM(ISNULL(a.TransactionID, ''))) = LTRIM(RTRIM(@GatewayTransactionId))
            ORDER BY a.LogDateTimeServer DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<int?>(new CommandDefinition(
            sql,
            new { GuardianId = guardianId, GatewayTransactionId = gatewayTransactionId.Trim() },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
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

    private sealed class PurchaseRow
    {
        public string SkuCode { get; init; } = string.Empty;

        public decimal Amount { get; init; }

        public DateTime? DeliveryDate { get; init; }
    }
}
