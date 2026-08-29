using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Data;
using System.Data.Common;
using Dapper;
using System.Text.Json;

namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TransactionRepository : ITransactionRepository
{
    private const int DefaultCommandTimeoutSeconds = 30;

    private const string PendingOrderIdExistsSql = """
        SELECT COUNT(1)
        FROM PendingTransInfo
        WHERE Remarks = @OrderId;
        """;

    private const string InsertPendingTransInfoSp = "spInsertPendingTransInfo";
    private const string UpdatePendingTransInfoSp = "spUpdatePendingTransInfo";
    private const string UpdateTopupTransInfoSp = "spUpdatePreOrderResTransInfo";
    private const string UpdatePrepaidBalanceSp = "spUpdatePrepaidBalance";
    private const string QueueEmailNotificationSp = "spQueueEmailNotification";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;

    public TransactionRepository(
        IDbConnectionFactory connectionFactory,
        IMealDbConnectionFactory mealDbConnectionFactory)
    {
        _connectionFactory = connectionFactory;
        _mealDbConnectionFactory = mealDbConnectionFactory;
    }

    public async Task<bool> PendingOrderIdExistsAsync(string orderId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var count = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                PendingOrderIdExistsSql,
                new { OrderId = orderId.Trim() },
                commandType: System.Data.CommandType.Text,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return count > 0;
    }

    public async Task LogPaymentRequestAsync(string transactionId, string result, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO PGLogs (TransactionId, Result, [Date])
            VALUES (@TransactionId, @Result, GETDATE());
            """;

        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            using var connection = _connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(timeoutCts.Token);

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    sql,
                    new
                    {
                        TransactionId = Truncate(transactionId, 50),
                        Result = Truncate(result, 4000)
                    },
                    commandTimeout: 15,
                    cancellationToken: timeoutCts.Token));
        }
        catch (Exception)
        {
            // Payment logging must never block or fail the payment flow.
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }

    public async Task InsertPendingTransactionAsync(PendingTransactionRequest request, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerID", request.CustomerID);
        parameters.Add("Creby", request.Creby);
        parameters.Add("Amount", request.Amount);
        parameters.Add("Loaded", request.Loaded);
        parameters.Add("TransDate", request.TransDate);
        parameters.Add("Remarks", request.Remarks);
        parameters.Add("Mode", string.IsNullOrWhiteSpace(request.Mode) ? "O" : request.Mode);
        parameters.Add("BankName", string.IsNullOrWhiteSpace(request.BankName) ? "ETISALAT" : request.BankName);
        parameters.Add("PaymentDetails", request.PaymentDetails);
        parameters.Add("Billdate", request.Billdate);
        parameters.Add("RequestObject", request.RequestObject);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                InsertPendingTransInfoSp,
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task UpdatePendingTransactionAsync(UpdatePendingTransactionRequest request, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerID", request.CustomerID);
        parameters.Add("Loaded", request.Loaded);
        parameters.Add("Creby", request.Creby);
        parameters.Add("PaymentDetails", request.PaymentDetails);
        parameters.Add("Remarks", request.Remarks);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePendingTransInfoSp,
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task UpdatePendingAndTopupTransactionAsync(
        UpdatePendingTransactionRequest pendingRequest,
        UpdateTopupTransactionRequest topupRequest,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        var pendingParameters = new DynamicParameters();
        pendingParameters.Add("CustomerID", pendingRequest.CustomerID);
        pendingParameters.Add("Loaded", pendingRequest.Loaded);
        pendingParameters.Add("Creby", pendingRequest.Creby);
        pendingParameters.Add("PaymentDetails", pendingRequest.PaymentDetails);
        pendingParameters.Add("Remarks", pendingRequest.Remarks);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePendingTransInfoSp,
                pendingParameters,
                transaction: transaction,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        var topupParameters = new DynamicParameters();
        topupParameters.Add("CustomerID", topupRequest.CustomerID);
        topupParameters.Add("Remarks", topupRequest.Remarks);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdateTopupTransInfoSp,
                topupParameters,
                transaction: transaction,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task UpdatePrepaidBalanceAsync(
        string customerId,
        decimal rechargeAmount,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("Customer ID is required.", nameof(customerId));
        }

        if (rechargeAmount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rechargeAmount), rechargeAmount, "Recharge amount must be greater than zero.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerID", customerId.Trim());
        parameters.Add("RechargeAmount", rechargeAmount);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePrepaidBalanceSp,
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task UpdateTopupTransactionAsync(UpdateTopupTransactionRequest request, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var parameters = new DynamicParameters();
        parameters.Add("CustomerID", request.CustomerID);
        parameters.Add("Remarks", request.Remarks);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdateTopupTransInfoSp,
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateTopupPendingTransactionAsync(TopupTransactionCreateRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO [Transaction]
                (GuardianId, StudentId, TransactionType, Amount, Remarks, IsTransactionCompleted, IsDebit, StatusId, CreatedOn, CreatedBy, ReconcileAttemptCount)
            VALUES
                (@GuardianId, @StudentId, NULL, @Amount, @Remarks, 0, 1, @StatusId, GETDATE(), @CreatedBy, 0);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        return await connection.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new
            {
                request.GuardianId,
                request.StudentId,
                request.Amount,
                request.Remarks,
                request.StatusId,
                request.CreatedBy
            },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task UpdateTopupTransactionStatusAsync(TopupTransactionUpdateRequest request, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [Transaction]
            SET TransactionId = @GatewayTransactionId,
                StatusId = @StatusId,
                IsTransactionCompleted = @IsTransactionCompleted,
                Remarks = CASE
                    WHEN @Remarks IS NULL OR LTRIM(RTRIM(@Remarks)) = '' THEN Remarks
                    ELSE @Remarks
                END,
                UpdatedOn = GETDATE(),
                UpdatedBy = @UpdatedBy
            WHERE Id = @TransactionPkId;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                request.TransactionPkId,
                request.GatewayTransactionId,
                request.StatusId,
                request.IsTransactionCompleted,
                request.Remarks,
                request.UpdatedBy
            },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<TopupPendingTransactionState?> GetTopupPendingByOrderIdAsync(
        string orderId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        return await QueryTopupPendingAsync(orderId, gatewayTransactionId, forUpdate: false, cancellationToken);
    }

    public async Task<TopupPendingTransactionState?> GetTopupPendingForCompletionAsync(
        string orderId,
        string? gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        return await QueryTopupPendingAsync(orderId, gatewayTransactionId, forUpdate: true, cancellationToken);
    }

    private async Task<TopupPendingTransactionState?> QueryTopupPendingAsync(
        string orderId,
        string? gatewayTransactionId,
        bool forUpdate,
        CancellationToken cancellationToken)
    {
        var lockHint = forUpdate ? "WITH (UPDLOCK, ROWLOCK)" : string.Empty;
        // Remarks should hold OrderId, but older rows may have overwritten it with status text.
        // Fall back: match MealDB.TransactionId to ibonus.PendingTransInfo.PaymentDetails by OrderId.
        var sql = $"""
            SELECT TOP (1)
                t.Id AS TransactionPkId,
                COALESCE(
                    NULLIF(LTRIM(RTRIM(p.Remarks)), ''),
                    NULLIF(LTRIM(RTRIM(t.Remarks)), ''),
                    @OrderId
                ) AS OrderId,
                IsTransactionCompleted = ISNULL(t.IsTransactionCompleted, 0),
                t.StatusId,
                ISNULL(t.Amount, 0) AS Amount,
                ISNULL(t.StudentId, 0) AS StudentId,
                ISNULL(t.GuardianId, 0) AS GuardianId,
                LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) AS GatewayTransactionId,
                t.AccessLogId
            FROM [Transaction] t {lockHint}
            LEFT JOIN [Order] o ON o.TransactionId = t.Id
            LEFT JOIN ibonus.dbo.PendingTransInfo p
                ON LTRIM(RTRIM(ISNULL(p.PaymentDetails, ''))) = LTRIM(RTRIM(ISNULL(t.TransactionId, '')))
               AND (
                    LTRIM(RTRIM(ISNULL(p.Remarks, ''))) = @OrderId
                    OR LTRIM(RTRIM(ISNULL(t.Remarks, ''))) = @OrderId
                   )
            WHERE o.Id IS NULL
              AND (
                    LTRIM(RTRIM(ISNULL(t.Remarks, ''))) = @OrderId
                    OR (
                        @GatewayTransactionId IS NOT NULL
                        AND LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) = @GatewayTransactionId
                    )
                    OR (
                        NULLIF(@OrderId, '') IS NOT NULL
                        AND LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) IN (
                            SELECT LTRIM(RTRIM(p2.PaymentDetails))
                            FROM ibonus.dbo.PendingTransInfo p2
                            WHERE LTRIM(RTRIM(ISNULL(p2.Remarks, ''))) = @OrderId
                              AND NULLIF(LTRIM(RTRIM(ISNULL(p2.PaymentDetails, ''))), '') IS NOT NULL
                        )
                    )
                  )
            ORDER BY t.Id DESC;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        if (forUpdate)
        {
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
            var state = await dbConnection.QueryFirstOrDefaultAsync<TopupPendingTransactionState>(new CommandDefinition(
                sql,
                new { OrderId = orderId.Trim(), GatewayTransactionId = gatewayTransactionId?.Trim() },
                transaction: transaction,
                commandType: System.Data.CommandType.Text,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
            return state;
        }

        return await connection.QueryFirstOrDefaultAsync<TopupPendingTransactionState>(new CommandDefinition(
            sql,
            new { OrderId = orderId.Trim(), GatewayTransactionId = gatewayTransactionId?.Trim() },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<PendingPaymentReconcileItem>> ListPendingForReconcileAsync(
        int lookbackHours,
        int maxAttempts,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (lookbackHours <= 0)
        {
            lookbackHours = 24;
        }

        if (maxAttempts <= 0)
        {
            maxAttempts = 3;
        }

        if (batchSize <= 0)
        {
            batchSize = 50;
        }

        const string sql = """
            SELECT TOP (@BatchSize)
                Kind,
                TransactionPkId,
                OrderId,
                GatewayTransactionId,
                StudentId,
                GuardianId,
                StatusId,
                ReconcileAttemptCount
            FROM
            (
                SELECT
                    1 AS Kind,
                    t.Id AS TransactionPkId,
                    LTRIM(RTRIM(ISNULL(t.Remarks, ''))) AS OrderId,
                    LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) AS GatewayTransactionId,
                    ISNULL(t.StudentId, 0) AS StudentId,
                    ISNULL(t.GuardianId, 0) AS GuardianId,
                    t.StatusId,
                    ISNULL(t.ReconcileAttemptCount, 0) AS ReconcileAttemptCount,
                    t.CreatedOn
                FROM [Transaction] t
                LEFT JOIN [Order] o ON o.TransactionId = t.Id
                WHERE o.Id IS NULL
                  AND ISNULL(t.IsTransactionCompleted, 0) = 0
                  AND t.StatusId IN (@StatusInitiated, @StatusPending)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(t.TransactionId, ''))), '') IS NOT NULL
                  AND NULLIF(LTRIM(RTRIM(ISNULL(t.Remarks, ''))), '') IS NOT NULL
                  AND t.CreatedOn >= DATEADD(HOUR, -@LookbackHours, SYSUTCDATETIME())
                  AND ISNULL(t.ReconcileAttemptCount, 0) < @MaxAttempts

                UNION ALL

                SELECT
                    2 AS Kind,
                    t.Id AS TransactionPkId,
                    LTRIM(RTRIM(ISNULL(o.OrderId, ''))) AS OrderId,
                    LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) AS GatewayTransactionId,
                    ISNULL(o.StudentId, 0) AS StudentId,
                    ISNULL(o.GuardianId, 0) AS GuardianId,
                    t.StatusId,
                    ISNULL(t.ReconcileAttemptCount, 0) AS ReconcileAttemptCount,
                    t.CreatedOn
                FROM [Order] o
                INNER JOIN [Transaction] t ON t.Id = o.TransactionId
                WHERE ISNULL(o.IsPaid, 0) = 0
                  AND ISNULL(t.IsTransactionCompleted, 0) = 0
                  AND t.StatusId IN (@StatusInitiated, @StatusPending)
                  AND NULLIF(LTRIM(RTRIM(ISNULL(t.TransactionId, ''))), '') IS NOT NULL
                  AND NULLIF(LTRIM(RTRIM(ISNULL(o.OrderId, ''))), '') IS NOT NULL
                  AND t.CreatedOn >= DATEADD(HOUR, -@LookbackHours, SYSUTCDATETIME())
                  AND ISNULL(t.ReconcileAttemptCount, 0) < @MaxAttempts
            ) pending
            ORDER BY CreatedOn ASC;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PendingPaymentReconcileRow>(new CommandDefinition(
            sql,
            new
            {
                LookbackHours = lookbackHours,
                MaxAttempts = maxAttempts,
                BatchSize = batchSize,
                StatusInitiated = (int)TransactionStatusEnum.Initiated,
                StatusPending = (int)TransactionStatusEnum.Pending
            },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        return rows
            .Where(r => r.StatusId is (int)TransactionStatusEnum.Initiated or (int)TransactionStatusEnum.Pending)
            .Select(r => new PendingPaymentReconcileItem
            {
                Kind = r.Kind == 2 ? PendingPaymentReconcileKind.Order : PendingPaymentReconcileKind.Topup,
                TransactionPkId = r.TransactionPkId,
                OrderId = r.OrderId?.Trim() ?? string.Empty,
                GatewayTransactionId = r.GatewayTransactionId?.Trim() ?? string.Empty,
                StudentId = r.StudentId,
                GuardianId = r.GuardianId,
                StatusId = r.StatusId,
                ReconcileAttemptCount = r.ReconcileAttemptCount
            })
            .ToList();
    }

    public async Task BumpReconcileAttemptAsync(int transactionPkId, CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [Transaction]
            SET ReconcileAttemptCount = ISNULL(ReconcileAttemptCount, 0) + 1,
                LastReconcileOn = SYSUTCDATETIME(),
                UpdatedOn = GETDATE()
            WHERE Id = @TransactionPkId
              AND StatusId IN (@StatusInitiated, @StatusPending)
              AND ISNULL(IsTransactionCompleted, 0) = 0;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new
            {
                TransactionPkId = transactionPkId,
                StatusInitiated = (int)TransactionStatusEnum.Initiated,
                StatusPending = (int)TransactionStatusEnum.Pending
            },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    private sealed class PendingPaymentReconcileRow
    {
        public int Kind { get; init; }

        public int TransactionPkId { get; init; }

        public string? OrderId { get; init; }

        public string? GatewayTransactionId { get; init; }

        public int StudentId { get; init; }

        public int GuardianId { get; init; }

        public int StatusId { get; init; }

        public int ReconcileAttemptCount { get; init; }
    }

    public async Task QueueEmailNotificationAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var payload = string.IsNullOrWhiteSpace(request.PayloadJson)
            ? JsonSerializer.Serialize(new
            {
                request.TemplateKey,
                request.ToEmail,
                request.GuardianName,
                request.StudentName,
                request.OrderId,
                request.TransactionId,
                request.Amount,
                request.EventDate,
                request.OrderItems,
                request.ResetLink,
                request.ExpiryMinutes,
                request.OtpCode,
                request.AddChildLink,
                request.LogoUrl,
                request.CardNumber,
                request.CustomerId,
                request.Reason,
                request.RefCode,
                request.SchoolName
            })
            : request.PayloadJson;

        var parameters = new DynamicParameters();
        parameters.Add("TemplateKey", request.TemplateKey);
        parameters.Add("ToEmail", request.ToEmail);
        parameters.Add("GuardianName", request.GuardianName);
        parameters.Add("StudentName", request.StudentName);
        parameters.Add("OrderId", request.OrderId);
        parameters.Add("TransactionId", request.TransactionId);
        parameters.Add("Amount", request.Amount);
        parameters.Add("EventDate", request.EventDate);
        parameters.Add("OrderItems", request.OrderItems);
        parameters.Add("ResetLink", request.ResetLink);
        parameters.Add("ExpiryMinutes", request.ExpiryMinutes);
        parameters.Add("OtpCode", request.OtpCode);
        parameters.Add("AddChildLink", request.AddChildLink);
        parameters.Add("LogoUrl", request.LogoUrl);
        parameters.Add("CardNumber", request.CardNumber);
        parameters.Add("CustomerId", request.CustomerId);
        parameters.Add("Reason", request.Reason);
        parameters.Add("RefCode", request.RefCode);
        parameters.Add("SchoolName", request.SchoolName);
        parameters.Add("PayloadJson", payload);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                QueueEmailNotificationSp,
                parameters,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task AttachAccessLogIdByTransactionPkAsync(
        int transactionPkId,
        long accessLogId,
        CancellationToken cancellationToken)
    {
        if (transactionPkId <= 0 || accessLogId <= 0)
        {
            return;
        }

        const string sql = """
            UPDATE [Transaction]
            SET AccessLogId = @AccessLogId,
                UpdatedOn = GETDATE()
            WHERE Id = @TransactionPkId;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { TransactionPkId = transactionPkId, AccessLogId = accessLogId },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<long?> GetAccessLogIdByTransactionPkAsync(
        int transactionPkId,
        CancellationToken cancellationToken)
    {
        if (transactionPkId <= 0)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1) AccessLogId
            FROM [Transaction]
            WHERE Id = @TransactionPkId;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<long?>(new CommandDefinition(
            sql,
            new { TransactionPkId = transactionPkId },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));
    }

    public async Task<TransactionHistoryResponse> GetTransactionHistoryAsync(
        int? studentId,
        int? guardianId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var normalizedType = string.IsNullOrWhiteSpace(type) ? "all" : type.Trim().ToLowerInvariant();

        if (normalizedType is not ("all" or "topup" or "order"))
        {
            throw new ArgumentException("Type must be one of: all, topup, order.", nameof(type));
        }

        DateTime? fromDateInclusive = fromDate?.Date;
        DateTime? toDateExclusive = toDate?.Date.AddDays(1);

        if (fromDateInclusive is DateTime from
            && toDateExclusive is DateTime toExclusive
            && from >= toExclusive)
        {
            // Invalid range: swap so filtering remains usable.
            fromDateInclusive = toDate?.Date;
            toDateExclusive = fromDate?.Date.AddDays(1);
        }

        // Driven from ibonus.AccessLog (all types for the guardian's students);
        // MealDB status/details via LEFT JOIN on gateway TransactionId.
        const string countSql = """
            SELECT COUNT(1)
            FROM ibonus.dbo.AccessLog a
            INNER JOIN ibonus.dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            WHERE (@GuardianId IS NULL OR sl.GrdId = @GuardianId)
              AND (@StudentId IS NULL OR CONVERT(int, sl.UserId) = @StudentId)
              AND (@FromDate IS NULL OR a.LogDateTimeServer >= @FromDate)
              AND (@ToDateExclusive IS NULL OR a.LogDateTimeServer < @ToDateExclusive)
              AND (
                    @Type = 'all'
                    OR (@Type = 'topup' AND a.TransactionType IN (23, 1004, 10001, 21004, 1007, 21007))
                    OR (@Type = 'order' AND a.TransactionType NOT IN (23, 1004, 10001, 21004, 1007, 21007))
                  );
            """;

        const string dataSql = """
            SELECT
                Id = ISNULL(t.Id, 0),
                AccessLogId = ISNULL(t.AccessLogId, 0),
                HasMealTransaction = CAST(CASE WHEN t.Id IS NULL THEN 0 ELSE 1 END AS bit),
                GuardianId = ISNULL(t.GuardianId, sl.GrdId),
                StudentId = CONVERT(int, sl.UserId),
                StudentName = CAST('' AS nvarchar(256)),
                TransactionType = CASE
                    WHEN a.TransactionType IN (23, 1004, 10001, 21004, 1007, 21007) THEN 'topup'
                    ELSE 'order'
                END,
                OrderTypeId = CASE
                    WHEN o.OrderTypeId IS NOT NULL THEN o.OrderTypeId
                    WHEN a.TransactionType = 24 THEN 24
                    WHEN a.TransactionType = 42 THEN 42
                    WHEN a.TransactionType = 61 THEN 24
                    WHEN a.TransactionType = 43 THEN 43
                    ELSE NULL
                END,
                OrderId = ISNULL(o.OrderId, ''),
                GatewayTransactionId = COALESCE(
                    NULLIF(LTRIM(RTRIM(ISNULL(t.TransactionId, ''))), ''),
                    NULLIF(LTRIM(RTRIM(ISNULL(a.TransactionID, ''))), ''),
                    ''),
                Amount = ISNULL(t.Amount, ISNULL(a.Amount, 0)),
                Remarks = COALESCE(
                    NULLIF(LTRIM(RTRIM(ISNULL(t.Remarks, ''))), ''),
                    NULLIF(LTRIM(RTRIM(ISNULL(a.Description, ''))), ''),
                    ''),
                IsTransactionCompleted = CASE
                    WHEN t.Id IS NULL THEN CAST(1 AS bit)
                    ELSE CAST(ISNULL(t.IsTransactionCompleted, 0) AS bit)
                END,
                StatusId = CASE
                    WHEN t.Id IS NULL THEN 21
                    ELSE t.StatusId
                END,
                CreatedOn = ISNULL(t.CreatedOn, a.LogDateTimeServer),
                UpdatedOn = t.UpdatedOn
            FROM ibonus.dbo.AccessLog a
            INNER JOIN ibonus.dbo.StudentLogin sl
                ON LTRIM(RTRIM(ISNULL(sl.CustomerID, ''))) = LTRIM(RTRIM(ISNULL(a.CustomerID, '')))
            LEFT JOIN [Transaction] t
                ON NULLIF(LTRIM(RTRIM(ISNULL(a.TransactionID, ''))), '') IS NOT NULL
               AND LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) = LTRIM(RTRIM(ISNULL(a.TransactionID, '')))
               AND (
                    ISNULL(t.StudentId, 0) = CONVERT(int, sl.UserId)
                    OR (@GuardianId IS NOT NULL AND t.GuardianId = @GuardianId)
                   )
            LEFT JOIN [Order] o ON o.TransactionId = t.Id
            WHERE (@GuardianId IS NULL OR sl.GrdId = @GuardianId)
              AND (@StudentId IS NULL OR CONVERT(int, sl.UserId) = @StudentId)
              AND (@FromDate IS NULL OR a.LogDateTimeServer >= @FromDate)
              AND (@ToDateExclusive IS NULL OR a.LogDateTimeServer < @ToDateExclusive)
              AND (
                    @Type = 'all'
                    OR (@Type = 'topup' AND a.TransactionType IN (23, 1004, 10001, 21004, 1007, 21007))
                    OR (@Type = 'order' AND a.TransactionType NOT IN (23, 1004, 10001, 21004, 1007, 21007))
                  )
            ORDER BY a.LogDateTimeServer DESC, a.TransactionID DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var offset = (page - 1) * pageSize;
        var filter = new
        {
            StudentId = studentId,
            GuardianId = guardianId,
            Type = normalizedType,
            FromDate = fromDateInclusive,
            ToDateExclusive = toDateExclusive
        };

        var totalCount = await connection.QuerySingleAsync<int>(new CommandDefinition(
            countSql,
            filter,
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<TransactionHistoryItemDto>(new CommandDefinition(
            dataSql,
            new
            {
                filter.StudentId,
                filter.GuardianId,
                filter.Type,
                filter.FromDate,
                filter.ToDateExclusive,
                Offset = offset,
                PageSize = pageSize
            },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken))).ToList();

        items = await EnrichStudentNamesAsync(items, cancellationToken);

        return new TransactionHistoryResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<TransactionHistoryItemDto?> GetGuardianTransactionByIdAsync(
        int guardianId,
        int transactionId,
        CancellationToken cancellationToken)
    {
        if (guardianId <= 0 || transactionId <= 0)
        {
            return null;
        }

        const string sql = """
            SELECT TOP (1)
                t.Id,
                AccessLogId = ISNULL(t.AccessLogId, 0),
                HasMealTransaction = CAST(1 AS bit),
                t.GuardianId,
                t.StudentId,
                StudentName = CAST('' AS nvarchar(256)),
                TransactionType = CASE WHEN o.Id IS NULL THEN 'topup' ELSE 'order' END,
                o.OrderTypeId,
                OrderId = ISNULL(o.OrderId, ''),
                GatewayTransactionId = ISNULL(t.TransactionId, ''),
                t.Amount,
                Remarks = ISNULL(t.Remarks, ''),
                IsTransactionCompleted = ISNULL(t.IsTransactionCompleted, 0),
                t.StatusId,
                t.CreatedOn,
                t.UpdatedOn
            FROM [Transaction] t
            LEFT JOIN [Order] o ON o.TransactionId = t.Id
            WHERE t.Id = @TransactionId
              AND t.GuardianId = @GuardianId;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var item = await connection.QueryFirstOrDefaultAsync<TransactionHistoryItemDto>(new CommandDefinition(
            sql,
            new { GuardianId = guardianId, TransactionId = transactionId },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        if (item is null)
        {
            return null;
        }

        var enriched = await EnrichStudentNamesAsync([item], cancellationToken);
        return enriched[0];
    }

    private async Task<List<TransactionHistoryItemDto>> EnrichStudentNamesAsync(
        List<TransactionHistoryItemDto> items,
        CancellationToken cancellationToken)
    {
        if (items.Count == 0)
        {
            return items;
        }

        var studentIds = items
            .Where(x => x.StudentId is > 0)
            .Select(x => x.StudentId!.Value)
            .Distinct()
            .ToList();

        if (studentIds.Count == 0)
        {
            return items;
        }

        const string namesSql = """
            SELECT
                sl.UserId AS StudentId,
                Name = LTRIM(RTRIM(
                    LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
                ))
            FROM StudentLogin sl
            WHERE sl.UserId IN @StudentIds;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var nameRows = await dbConnection.QueryAsync<(int StudentId, string Name)>(new CommandDefinition(
            namesSql,
            new { StudentIds = studentIds },
            commandType: System.Data.CommandType.Text,
            commandTimeout: DefaultCommandTimeoutSeconds,
            cancellationToken: cancellationToken));

        var nameMap = nameRows.ToDictionary(x => x.StudentId, x => x.Name?.Trim() ?? string.Empty);

        return items
            .Select(item =>
            {
                var studentName = item.StudentId is > 0 && nameMap.TryGetValue(item.StudentId.Value, out var name)
                    ? name
                    : string.Empty;

                if (string.Equals(item.StudentName, studentName, StringComparison.Ordinal))
                {
                    return item;
                }

                return new TransactionHistoryItemDto
                {
                    Id = item.Id,
                    AccessLogId = item.AccessLogId,
                    HasMealTransaction = item.HasMealTransaction,
                    GuardianId = item.GuardianId,
                    StudentId = item.StudentId,
                    StudentName = studentName,
                    TransactionType = item.TransactionType,
                    OrderTypeId = item.OrderTypeId,
                    OrderId = item.OrderId,
                    GatewayTransactionId = item.GatewayTransactionId,
                    Amount = item.Amount,
                    Remarks = item.Remarks,
                    IsTransactionCompleted = item.IsTransactionCompleted,
                    StatusId = item.StatusId,
                    CreatedOn = item.CreatedOn,
                    UpdatedOn = item.UpdatedOn
                };
            })
            .ToList();
    }
}
