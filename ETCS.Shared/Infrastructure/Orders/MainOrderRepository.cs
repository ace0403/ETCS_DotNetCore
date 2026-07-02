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
                (CustomerID, LogDateTimeTerminal, LogDateTimeServer, TransactionType, Description, Amount, BalPrepaid, AccSpending, TransactionID, TerminalCode, CompanyCode)
            VALUES
                (@CustomerID, GETDATE(), GETDATE(), @TransactionType, @Description, @Amount, @BalPrepaid, @AccSpending, @TransactionID, @TerminalCode, @CompanyCode);
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
}
