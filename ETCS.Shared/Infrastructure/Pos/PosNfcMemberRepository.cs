using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosNfcMemberRepository : IPosNfcMemberRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PosNfcMemberRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<PosNfcMemberRow?> FindByCardSnAsync(
        IReadOnlyList<string> cardSnCandidates,
        CancellationToken cancellationToken)
    {
        if (cardSnCandidates.Count == 0)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                LTRIM(RTRIM(ISNULL(m.CustomerID, ''))) AS CustomerId,
                LTRIM(RTRIM(ISNULL(m.CardSN, ''))) AS CardSn,
                CAST(ISNULL(m.BalPrepaid, 0) AS decimal(18,2)) AS BalPrepaid,
                CAST(ISNULL(m.AccSpending, 0) AS decimal(18,2)) AS AccSpending,
                CAST(ISNULL(m.IdCardStatus, 0) AS int) AS IdCardStatus
            FROM IdMember m
            WHERE REPLACE(REPLACE(REPLACE(REPLACE(UPPER(LTRIM(RTRIM(ISNULL(m.CardSN, '')))), ' ', ''), ':', ''), '-', ''), '.', '') IN @Candidates
            ORDER BY CASE WHEN m.IdCardStatus = 1 THEN 0 ELSE 1 END;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<PosNfcMemberRow>(new CommandDefinition(
            sql,
            new { Candidates = cardSnCandidates },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<PosNfcPurchaseResponse> PurchaseAsync(
        PosNfcMemberRow member,
        decimal amount,
        string transactionId,
        string ipAddress,
        DateTime purchaseDate,
        IReadOnlyList<PosPostPurchaseLineRequest> lines,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var debitRows = await dbConnection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE IdMember
                SET BalPrepaid = BalPrepaid - @Amount,
                    AccSpending = ISNULL(AccSpending, 0) + @Amount,
                    LastVisit = GETDATE()
                WHERE IdCardStatus = 1
                  AND LTRIM(RTRIM(CustomerID)) = LTRIM(RTRIM(@CustomerId))
                  AND BalPrepaid >= @Amount;
                """,
                new { member.CustomerId, Amount = amount },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (debitRows != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Fail("Insufficient balance. Current balance: AED " + member.BalPrepaid.ToString("0.00", CultureInfo.InvariantCulture) + ".", "INSUFFICIENT_BALANCE", member);
            }

            foreach (var line in lines)
            {
                if (string.IsNullOrWhiteSpace(line.SkuCode) || line.Amount <= 0)
                {
                    continue;
                }

                await dbConnection.ExecuteAsync(new CommandDefinition(
                    "spInsertWindposPurchase",
                    new
                    {
                        customerid = member.CustomerId,
                        skucode = line.SkuCode.Trim(),
                        amount = line.Amount.ToString("F2", CultureInfo.InvariantCulture),
                        purchasedate = purchaseDate.ToString(CultureInfo.InvariantCulture),
                        transid = transactionId,
                        ipaddress = ipAddress
                    },
                    transaction: transaction,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
            }

            var remaining = await dbConnection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                """
                SELECT CAST(ISNULL(BalPrepaid, 0) AS decimal(18,2))
                FROM IdMember
                WHERE IdCardStatus = 1
                  AND LTRIM(RTRIM(CustomerID)) = LTRIM(RTRIM(@CustomerId));
                """,
                new { member.CustomerId },
                transaction: transaction,
                cancellationToken: cancellationToken)) ?? 0m;

            await transaction.CommitAsync(cancellationToken);
            return new PosNfcPurchaseResponse
            {
                IsSuccess = true,
                Message = "NFC cashless transaction successful.",
                CustomerId = member.CustomerId,
                TransactionId = transactionId,
                CardSn = member.CardSn,
                Balance = remaining
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<PosNfcPurchaseResponse> UndoAsync(
        PosNfcMemberRow member,
        decimal amount,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var creditRows = await dbConnection.ExecuteAsync(new CommandDefinition(
                """
                UPDATE IdMember
                SET BalPrepaid = ISNULL(BalPrepaid, 0) + @Amount,
                    AccSpending = CASE
                        WHEN ISNULL(AccSpending, 0) >= @Amount THEN AccSpending - @Amount
                        ELSE 0
                    END
                WHERE IdCardStatus = 1
                  AND LTRIM(RTRIM(CustomerID)) = LTRIM(RTRIM(@CustomerId));
                """,
                new { member.CustomerId, Amount = amount },
                transaction: transaction,
                cancellationToken: cancellationToken));

            if (creditRows != 1)
            {
                await transaction.RollbackAsync(cancellationToken);
                return Fail("Unable to credit the card balance.", "CREDIT_FAILED", member);
            }

            await dbConnection.ExecuteAsync(new CommandDefinition(
                "spDeleteAccesslogBylimit",
                new
                {
                    customerid = member.CustomerId,
                    amount
                },
                transaction: transaction,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

            var remaining = await dbConnection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                """
                SELECT CAST(ISNULL(BalPrepaid, 0) AS decimal(18,2))
                FROM IdMember
                WHERE IdCardStatus = 1
                  AND LTRIM(RTRIM(CustomerID)) = LTRIM(RTRIM(@CustomerId));
                """,
                new { member.CustomerId },
                transaction: transaction,
                cancellationToken: cancellationToken)) ?? 0m;

            await transaction.CommitAsync(cancellationToken);
            return new PosNfcPurchaseResponse
            {
                IsSuccess = true,
                Message = "NFC cashless undo successful.",
                CustomerId = member.CustomerId,
                TransactionId = transactionId,
                CardSn = member.CardSn,
                Balance = remaining
            };
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static PosNfcPurchaseResponse Fail(string message, string code, PosNfcMemberRow member) =>
        new()
        {
            IsSuccess = false,
            Message = message,
            Code = code,
            CustomerId = member.CustomerId,
            CardSn = member.CardSn,
            Balance = member.BalPrepaid
        };
}
