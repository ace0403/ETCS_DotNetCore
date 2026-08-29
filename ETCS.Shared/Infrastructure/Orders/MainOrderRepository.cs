using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Orders;

public sealed class MainOrderRepository : IMainOrderRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public MainOrderRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<MemberFinancialProfile?> GetMemberFinancialProfileAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                Balance = ISNULL(BalPrepaid, 0)
            FROM IdMember
            WHERE CustomerID = @CustomerID
              AND IdCardStatus = 1;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<MemberFinancialProfile>(new CommandDefinition(
            sql,
            new { CustomerID = customerId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public Task<long> ApplySuccessfulOrderAsync(
        string customerId,
        string orderId,
        string gatewayTransactionId,
        decimal total,
        string notes,
        short accessLogTransactionType,
        string accessLogDescription,
        string terminalCode,
        string companyCode,
        CancellationToken cancellationToken) =>
        InsertAccessLogAsync(
            customerId,
            total,
            accessLogTransactionType,
            accessLogDescription,
            gatewayTransactionId,
            terminalCode,
            companyCode,
            cancellationToken);

    public async Task<long> InsertAccessLogAsync(
        string customerId,
        decimal amount,
        short accessLogTransactionType,
        string description,
        string transactionId,
        string terminalCode,
        string companyCode,
        CancellationToken cancellationToken)
    {
        const string insertAccessLogSql = """
            INSERT INTO AccessLog
                (CustomerID, LogDateTimeTerminal, LogDateTimeServer, TransactionType, Description, Amount, BalPrepaid, AccSpending, TransactionID, TerminalCode, CompanyCode, BranchCode)
            VALUES
                (@CustomerID, GETDATE(), GETDATE(), @TransactionType, @Description, @Amount, @BalPrepaid, @AccSpending, @TransactionID, @TerminalCode, @CompanyCode,
                 (
                    SELECT TOP (1)
                        TRY_CAST(NULLIF(LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))), '') AS smallint)
                    FROM StudentLogin sl
                    LEFT JOIN SchoolInfo s
                        ON s.SchoolId = TRY_CONVERT(INT, NULLIF(LTRIM(RTRIM(CONVERT(varchar(50), sl.StudSchoolId))), ''))
                        OR (
                            LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) <> ''
                            AND LTRIM(RTRIM(s.Schoolcode)) = LTRIM(RTRIM(CONVERT(varchar(50), sl.StudSchoolId)))
                        )
                    WHERE LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = LTRIM(RTRIM(@CustomerID))
                 ));
            SELECT CAST(SCOPE_IDENTITY() AS bigint);
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var accessLogId = await dbConnection.QuerySingleAsync<long>(new CommandDefinition(
                insertAccessLogSql,
                new
                {
                    CustomerID = customerId,
                    TransactionType = accessLogTransactionType,
                    Description = description,
                    Amount = amount,
                    BalPrepaid = amount,
                    AccSpending = 0,
                    TransactionID = transactionId,
                    TerminalCode = terminalCode,
                    CompanyCode = companyCode,
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return accessLogId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<long?> FindAccessLogIdByGatewayTransactionAsync(
        string customerId,
        string gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return null;
        }

        // AccessLog identity column name is legacy/unknown; resolve via sys.identity_columns.
        const string sql = """
            DECLARE @col sysname =
            (
                SELECT TOP (1) c.name
                FROM sys.identity_columns c
                WHERE c.object_id = OBJECT_ID(N'dbo.AccessLog')
            );

            IF @col IS NULL
            BEGIN
                SELECT CAST(NULL AS bigint);
                RETURN;
            END

            DECLARE @sql nvarchar(max) = N'
                SELECT TOP (1) CONVERT(bigint, a.' + QUOTENAME(@col) + N')
                FROM dbo.AccessLog a
                WHERE LTRIM(RTRIM(ISNULL(a.CustomerID, ''''))) = LTRIM(RTRIM(@CustomerID))
                  AND LTRIM(RTRIM(ISNULL(a.TransactionID, ''''))) = LTRIM(RTRIM(@TransactionID))
                ORDER BY a.LogDateTimeServer DESC';

            EXEC sp_executesql
                @sql,
                N'@CustomerID nvarchar(75), @TransactionID nvarchar(100)',
                @CustomerID = @CustomerID,
                @TransactionID = @TransactionID;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            sql,
            new
            {
                CustomerID = customerId.Trim(),
                TransactionID = gatewayTransactionId.Trim()
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }
}
