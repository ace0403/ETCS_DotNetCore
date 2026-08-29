using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Students;

public sealed class ReplaceCardRequestRepository : IReplaceCardRequestRepository
{
    public const string ParentAppTerminalSerialNo = "PARENT_APP";

    private const string OwnsCustomerSql = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM StudentLogin sl
            WHERE sl.GrdId = @GuardianId
              AND sl.CustomerId = @CustomerId
        ) THEN 1 ELSE 0 END AS bit);
        """;

    private const string PendingExistsSql = """
        SELECT CAST(CASE WHEN EXISTS (
            SELECT 1
            FROM ReplaceRequests rr
            WHERE LTRIM(RTRIM(ISNULL(rr.CustomerID, ''))) = LTRIM(RTRIM(@CustomerId))
              AND ISNULL(rr.Status, -1) = 0
        ) THEN 1 ELSE 0 END AS bit);
        """;

    // IdMember does not expose CardID / MemberInfo / DateOfBirth / RecordExpiryDate;
    // those ReplaceRequests columns are left NULL (except CardID, filled from request cardNumber).
    private const string IdMemberByCustomerSql = """
        SELECT TOP (1)
            m.AccSpending,
            m.BalBonus,
            CAST(ISNULL(m.BalPrepaid, 0) AS decimal(17,2)) AS BalPrepaid,
            LTRIM(RTRIM(ISNULL(m.CustomerID, ''))) AS CustomerID,
            m.TransactionCounter,
            m.ExpiryDate
        FROM IdMember m
        WHERE m.IdCardStatus = 1
          AND LTRIM(RTRIM(m.CardSN)) = LTRIM(RTRIM(@CardSN));
        """;

    private const string NextRefCodeSql = """
        SELECT ISNULL(MAX(RefCode), 0) + 1
        FROM ReplaceRequests WITH (UPDLOCK, HOLDLOCK)
        WHERE RTRIM(TerminalSerialNo) = RTRIM(@TerminalSerialNo);
        """;

    private const string InsertReplaceRequestSql = """
        INSERT INTO ReplaceRequests (
            TerminalSerialNo,
            RefCode,
            CardSN,
            Status,
            TimeChgState,
            RequiredBlacklistVersion,
            ExpiryDate,
            CardID,
            AccSpending,
            MemberInfo,
            DateOfBirth,
            BalBonus,
            BalPrepaid,
            CustomerID,
            TransactionCounter,
            RecordExpiryDate,
            Reason)
        VALUES (
            @TerminalSerialNo,
            @RefCode,
            @CardSN,
            @Status,
            GETDATE(),
            NULL,
            @ExpiryDate,
            @CardID,
            @AccSpending,
            @MemberInfo,
            @DateOfBirth,
            @BalBonus,
            @BalPrepaid,
            @CustomerID,
            @TransactionCounter,
            @RecordExpiryDate,
            @Reason);
        """;

    private const string ListByGuardianSql = """
        SELECT
            rr.RefCode,
            LTRIM(RTRIM(ISNULL(rr.CustomerID, ''))) AS CustomerId,
            LTRIM(RTRIM(ISNULL(rr.CardID, ''))) AS CardNumber,
            LTRIM(RTRIM(ISNULL(rr.CardID, ''))) AS CardId,
            LTRIM(RTRIM(ISNULL(rr.CardSN, ''))) AS CardSn,
            rr.Status,
            rr.TimeChgState,
            rr.BalPrepaid,
            rr.ExpiryDate,
            rr.RecordExpiryDate,
            LTRIM(RTRIM(ISNULL(rr.Reason, ''))) AS Reason
        FROM ReplaceRequests rr
        INNER JOIN StudentLogin sl
            ON LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = LTRIM(RTRIM(ISNULL(rr.CustomerID, '')))
        WHERE sl.GrdId = @GuardianId
        ORDER BY rr.TimeChgState DESC;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public ReplaceCardRequestRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ReplaceCardCreateResult> CreateAsync(
        int guardianId,
        string customerId,
        string cardNumber,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0)
        {
            return new ReplaceCardCreateResult(false, "Guardian is required.");
        }

        var normalizedCustomerId = customerId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCustomerId))
        {
            return new ReplaceCardCreateResult(false, "CustomerId is required.");
        }

        if (normalizedCustomerId.Length > 16)
        {
            return new ReplaceCardCreateResult(false, "CustomerId exceeds maximum length.");
        }

        var normalizedCardNumber = cardNumber.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCardNumber))
        {
            return new ReplaceCardCreateResult(false, "CardNumber is required.");
        }

        if (normalizedCardNumber.Length > 20)
        {
            return new ReplaceCardCreateResult(false, "CardNumber exceeds maximum length.");
        }

        var normalizedReason = reason.Trim();
        if (string.IsNullOrWhiteSpace(normalizedReason))
        {
            return new ReplaceCardCreateResult(false, "Reason is required.");
        }

        if (normalizedReason.Length > 500)
        {
            return new ReplaceCardCreateResult(false, "Reason exceeds maximum length of 500 characters.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var ownsCustomer = await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                OwnsCustomerSql,
                new { GuardianId = guardianId, CustomerId = normalizedCustomerId },
                cancellationToken: cancellationToken));

        if (!ownsCustomer)
        {
            return new ReplaceCardCreateResult(false, "Student card was not found for this guardian.");
        }

        var pendingExists = await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                PendingExistsSql,
                new { CustomerId = normalizedCustomerId },
                cancellationToken: cancellationToken));

        if (pendingExists)
        {
            return new ReplaceCardCreateResult(false, "A pending replace-card request already exists for this card.");
        }

        var member = await dbConnection.QueryFirstOrDefaultAsync<IdMemberRow>(
            new CommandDefinition(
                IdMemberByCustomerSql,
                new { CardSN = normalizedCardNumber },
                cancellationToken: cancellationToken));

        if (member is null)
        {
            return new ReplaceCardCreateResult(false, "Active prepaid card wallet was not found for this CustomerId.");
        }

        var terminalSerialNo = PadTerminalSerialNo(ParentAppTerminalSerialNo);
        var storedCardNumber = TruncateFixed(normalizedCardNumber, 20);

        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
        try
        {
            var refCode = await dbConnection.ExecuteScalarAsync<int>(
                new CommandDefinition(
                    NextRefCodeSql,
                    new { TerminalSerialNo = terminalSerialNo },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    InsertReplaceRequestSql,
                    new
                    {
                        TerminalSerialNo = terminalSerialNo,
                        RefCode = refCode,
                        CardSN = storedCardNumber,
                        Status = (short)0,
                        ExpiryDate = member.ExpiryDate,
                        CardID = storedCardNumber,
                        AccSpending = member.AccSpending,
                        MemberInfo = (int?)null,
                        DateOfBirth = (DateTime?)null,
                        BalBonus = member.BalBonus,
                        BalPrepaid = member.BalPrepaid,
                        CustomerID = TruncateFixed(normalizedCustomerId, 16),
                        TransactionCounter = member.TransactionCounter,
                        RecordExpiryDate = (DateTime?)null,
                        Reason = normalizedReason
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
            return new ReplaceCardCreateResult(true, "Replace card request submitted.", refCode);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<IReadOnlyList<ReplaceCardRequestListItemDto>> GetByGuardianAsync(
        int guardianId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0)
        {
            return Array.Empty<ReplaceCardRequestListItemDto>();
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = (await dbConnection.QueryAsync<ReplaceCardRequestListItemDto>(
            new CommandDefinition(
                ListByGuardianSql,
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken))).ToList();

        return rows
            .Select(row => new ReplaceCardRequestListItemDto
            {
                RefCode = row.RefCode,
                CustomerId = row.CustomerId.Trim(),
                CardNumber = string.IsNullOrWhiteSpace(row.CardNumber) ? null : row.CardNumber.Trim(),
                CardId = string.IsNullOrWhiteSpace(row.CardId) ? null : row.CardId.Trim(),
                CardSn = string.IsNullOrWhiteSpace(row.CardSn) ? null : row.CardSn.Trim(),
                Status = row.Status,
                TimeChgState = row.TimeChgState,
                BalPrepaid = row.BalPrepaid,
                ExpiryDate = row.ExpiryDate,
                RecordExpiryDate = row.RecordExpiryDate,
                Reason = string.IsNullOrWhiteSpace(row.Reason) ? null : row.Reason.Trim()
            })
            .ToList();
    }

    private static string PadTerminalSerialNo(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 20)
        {
            return trimmed[..20];
        }

        return trimmed.PadRight(20);
    }

    private static string TruncateFixed(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private sealed class IdMemberRow
    {
        public decimal? AccSpending { get; init; }

        public int? BalBonus { get; init; }

        public decimal BalPrepaid { get; init; }

        public string? CustomerID { get; init; }

        public int? TransactionCounter { get; init; }

        public int? ExpiryDate { get; init; }
    }
}
