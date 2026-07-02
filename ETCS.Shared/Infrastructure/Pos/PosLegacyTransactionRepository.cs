using System.Data;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosLegacyTransactionRepository : IPosLegacyTransactionRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PosLegacyTransactionRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> InsertCashPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "spInsertCashPurcahse",
            new
            {
                Amount = (float)amount,
                branchCode,
                terminalcode = terminalCode,
                transactionid = transactionId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return true;
    }

    public async Task<bool> UndoCashPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "spUndoCashPurhcase",
            new
            {
                Amount = (float)amount,
                branchCode,
                terminalcode = terminalCode,
                transactionid = transactionId
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return true;
    }

    public async Task<bool> InsertCardPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        string creditCardNumber,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "spInsertCreditCardPurcahse",
            new
            {
                Amount = (float)amount,
                branchCode,
                terminalcode = terminalCode,
                transactionid = transactionId,
                CreditCardNumber = creditCardNumber
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return true;
    }

    public async Task<bool> InsertPosPurchaseLineAsync(
        string customerId,
        string skuCode,
        decimal amount,
        DateTime purchaseDate,
        string transactionId,
        string ipAddress,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "spInsertWindposPurchase",
            new
            {
                customerid = customerId,
                skucode = skuCode,
                amount = amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
                purchasedate = purchaseDate.ToString(System.Globalization.CultureInfo.InvariantCulture),
                transid = transactionId,
                ipaddress = ipAddress
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return true;
    }

    public async Task<bool> RollbackSpendLimitAsync(
        string customerId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            "spDeleteAccesslogBylimit",
            new
            {
                customerid = customerId,
                amount
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));
        return true;
    }

    public async Task<PosLegacySpendLimitRow?> GetSpendLimitInfoAsync(
        string customerId,
        DateTime currentDate,
        DateTime weekStartDate,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var table = await connection.QueryAsync<dynamic>(new CommandDefinition(
            "spGetSpendLimitInfo",
            new
            {
                customerid = customerId,
                currentDate = currentDate.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                Starttime = weekStartDate.ToString(System.Globalization.CultureInfo.InvariantCulture)
            },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken));

        var row = table.FirstOrDefault();
        if (row is null)
        {
            return null;
        }

        var dict = (IDictionary<string, object>)row;
        return new PosLegacySpendLimitRow
        {
            DailyPurchaseAmount = ToDecimal(dict.Values.ElementAtOrDefault(0)),
            DailyUndoPurchaseAmount = ToDecimal(dict.Values.ElementAtOrDefault(1)),
            WeeklyPurchaseAmount = ToDecimal(dict.Values.ElementAtOrDefault(2)),
            WeeklyUndoPurchaseAmount = ToDecimal(dict.Values.ElementAtOrDefault(3)),
            DailySpendLimit = ToDecimal(dict.Values.ElementAtOrDefault(4)),
            WeeklySpendLimit = ToDecimal(dict.Values.ElementAtOrDefault(5))
        };
    }

    public async Task<IReadOnlyDictionary<int, string>> GetItemCodesByMealItemIdsAsync(
        IReadOnlyList<int> mealItemIds,
        CancellationToken cancellationToken)
    {
        if (mealItemIds.Count == 0)
        {
            return new Dictionary<int, string>();
        }

        using var connection = _connectionFactory.CreateConnection();
        try
        {
            const string sql = """
                SELECT
                    im.ItemId,
                    LTRIM(RTRIM(CAST(im.ItemCode AS varchar(20)))) AS ItemCode
                FROM ItemMaster im
                WHERE im.ItemId IN @Ids;
                """;

            var rows = await connection.QueryAsync<ItemCodeRow>(new CommandDefinition(
                sql,
                new { Ids = mealItemIds },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

            return rows
                .Where(r => r.ItemId > 0 && !string.IsNullOrWhiteSpace(r.ItemCode))
                .ToDictionary(r => r.ItemId, r => r.ItemCode);
        }
        catch
        {
            var map = new Dictionary<int, string>();
            foreach (var id in mealItemIds.Distinct())
            {
                try
                {
                    var code = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
                        "spGetItemCodeByItemId",
                        new { ItemId = id },
                        commandType: CommandType.StoredProcedure,
                        cancellationToken: cancellationToken));
                    if (!string.IsNullOrWhiteSpace(code))
                    {
                        map[id] = code.Trim();
                    }
                }
                catch
                {
                    map[id] = id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
            }

            return map;
        }
    }

    private static decimal ToDecimal(object? value)
    {
        if (value is null || value is DBNull)
        {
            return 0m;
        }

        return decimal.TryParse(value.ToString(), out var parsed) ? parsed : 0m;
    }

    private sealed class ItemCodeRow
    {
        public int ItemId { get; init; }
        public string ItemCode { get; init; } = string.Empty;
    }
}
