using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Orders;

public sealed class MealOrderRepository : IMealOrderRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public MealOrderRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> OrderIdExistsAsync(string orderId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT COUNT(1)
            FROM [Order]
            WHERE OrderId = @OrderId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var count = await connection.QuerySingleAsync<int>(new CommandDefinition(
            sql,
            new { OrderId = orderId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return count > 0;
    }

    public async Task<OrderSpendingSnapshot> GetSpendingSnapshotAsync(
        int studentId,
        int guardianId,
        DateTime referenceDate,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                DailySpent = ISNULL(SUM(CASE WHEN CAST(o.CreatedOn AS date) = @RefDate THEN o.Total ELSE 0 END), 0),
                WeeklySpent = ISNULL(SUM(CASE WHEN o.CreatedOn >= @WeekStart AND o.CreatedOn < @WeekEnd THEN o.Total ELSE 0 END), 0)
            FROM [Order] o
            WHERE o.StudentId = @StudentId
              AND o.GuardianId = @GuardianId;
            """;

        var refDate = referenceDate.Date;
        var weekStart = refDate.AddDays(-(int)refDate.DayOfWeek);
        var weekEnd = weekStart.AddDays(7);

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QuerySingleAsync<OrderSpendingSnapshot>(
            new CommandDefinition(
                sql,
                new
                {
                    StudentId = studentId,
                    GuardianId = guardianId,
                    RefDate = refDate,
                    WeekStart = weekStart,
                    WeekEnd = weekEnd
                },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreatePendingOrderAsync(
        OrderInitiateRequest request,
        int transactionStatusId,
        CancellationToken cancellationToken)
    {
        const string insertTransactionSql = """
            INSERT INTO [Transaction]
                (GuardianId, StudentId, TransactionType, Amount, Remarks, IsTransactionCompleted, IsDebit, StatusId, CreatedOn, CreatedBy)
            VALUES
                (@GuardianId, @StudentId, NULL, @Amount, @Remarks, 0, 1, @StatusId, GETDATE(), @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        const string insertOrderSql = """
            INSERT INTO [Order]
                (OrderId, TransactionId, OrderTypeId, OrderStatusId, OrderDate, StudentId, GuardianId, SubTotal, TaxAmount, Total, TotalItems, Notes, IsPaid, CreatedOn, CreatedBy)
            VALUES
                (@OrderId, @TransactionId, @OrderTypeId, @OrderStatusId, GETDATE(), @StudentId, @GuardianId, @SubTotal, 0, @Total, @TotalItems, @Notes, 0, GETDATE(), @CreatedBy);
            SELECT CAST(SCOPE_IDENTITY() AS int);
            """;

        const string insertItemSql = """
            INSERT INTO [OrderItem]
                (OrderId, ItemId, PackageId, ItemPrice, Total, Quantity, MealDate, CreatedOn)
            VALUES
                (@OrderId, @ItemId, @PackageId, @Price, @Total, @Quantity, @MealDate, GETDATE());
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var lineParams = new List<object>();
            var mealTransactionId = await dbConnection.QuerySingleAsync<int>(new CommandDefinition(
                insertTransactionSql,
                new
                {
                    request.GuardianId,
                    request.StudentId,
                    Amount = request.Total,
                    Remarks = request.OrderId,
                    StatusId = transactionStatusId,
                    CreatedBy = request.GuardianId
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            var dborderId = await dbConnection.QuerySingleAsync<int>(new CommandDefinition(
                insertOrderSql,
                new
                {
                    request.OrderId,
                    TransactionId = mealTransactionId,
                    request.OrderTypeId,
                    request.OrderStatusId,
                    request.StudentId,
                    request.GuardianId,
                    SubTotal = request.Total,
                    request.Total,
                    TotalItems = request.MealList.Count,
                    request.Notes,
                    CreatedBy = request.GuardianId
                },
                transaction: transaction,
                cancellationToken: cancellationToken));

            foreach (var line in request.MealList)
            {
                lineParams.Add(new
                {
                    OrderId = dborderId,
                    ItemId = request.OrderTypeId == (int)TransactionTypeEnum.A_La_Carte ? line.ItemId : null,
                    PackageId = request.OrderTypeId == (int)TransactionTypeEnum.MealOrder ? line.PackageId : null,
                    MealDate = line.MealDate.Date,
                    Price = line.Price,
                    Total = line.Total,
                    Quantity = line.Quantity
                });
            }

            if (lineParams.Count > 0)
            {
                await dbConnection.ExecuteAsync(new CommandDefinition(
                    insertItemSql,
                    lineParams,
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            }

            await transaction.CommitAsync(cancellationToken);
            return mealTransactionId;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task SetPaymentSessionAsync(
        string orderId,
        string gatewayTransactionId,
        int transactionStatusId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [Transaction]
            SET TransactionId = @GatewayTransactionId,
                StatusId = @StatusId,
                UpdatedOn = GETDATE()
            WHERE Id = (SELECT TOP (1) TransactionId FROM [Order] WHERE OrderId = @OrderId);
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { OrderId = orderId, GatewayTransactionId = gatewayTransactionId, StatusId = transactionStatusId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task SetPaymentSessionFailedAsync(
        string orderId,
        string message,
        CancellationToken cancellationToken)
    {
        const string updateTransactionSql = """
            UPDATE [Transaction]
            SET Remarks = @Message,
                UpdatedOn = GETDATE(),
                StatusId = @StatusId
            WHERE Id = (SELECT TOP (1) TransactionId FROM [Order] WHERE OrderId = @OrderId);
            """;

        const string updateOrderSql = """
            UPDATE [Order]
            SET OrderStatusId = @StatusId,
                UpdatedOn = GETDATE()
            WHERE OrderId = @OrderId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            var args = new
            {
                OrderId = orderId,
                Message = message,
                StatusId = (int)TransactionStatusEnum.Failed
            };

            await dbConnection.ExecuteAsync(new CommandDefinition(
                updateTransactionSql,
                args,
                transaction: transaction,
                cancellationToken: cancellationToken));

            await dbConnection.ExecuteAsync(new CommandDefinition(
                updateOrderSql,
                args,
                transaction: transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<MealOrderPaymentState?> GetPaymentStateAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                o.OrderId,
                o.StudentId,
                o.GuardianId,
                o.Total,
                o.OrderTypeId,
                IsPaid = ISNULL(o.IsPaid, 0),
                t.AccessLogId,
                t.Id AS MealTransactionId,
                TransactionStatusId = ISNULL(t.StatusId, 0),
                IsTransactionCompleted = ISNULL(t.IsTransactionCompleted, 0)
            FROM [Order] o
            LEFT JOIN [Transaction] t ON t.Id = o.TransactionId
            WHERE o.OrderId = @OrderId
            ORDER BY t.Id DESC;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<MealOrderPaymentState>(new CommandDefinition(
            sql,
            new { OrderId = orderId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<string?> GetGatewayTransactionIdByOrderIdAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1) t.TransactionId
            FROM [Order] o
            INNER JOIN [Transaction] t ON t.Id = o.TransactionId
            WHERE o.OrderId = @OrderId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<string>(new CommandDefinition(
            sql,
            new { OrderId = orderId.Trim() },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<MealOrderPaymentState?> GetPaymentStateForCompletionAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                o.OrderId,
                o.StudentId,
                o.GuardianId,
                o.Total,
                o.OrderTypeId,
                IsPaid = ISNULL(o.IsPaid, 0),
                t.AccessLogId,
                t.Id AS MealTransactionId,
                TransactionStatusId = ISNULL(t.StatusId, 0),
                IsTransactionCompleted = ISNULL(t.IsTransactionCompleted, 0)
            FROM [Order] o WITH (UPDLOCK, ROWLOCK)
            LEFT JOIN [Transaction] t ON t.Id = o.TransactionId
            WHERE o.OrderId = @OrderId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        var state = await dbConnection.QueryFirstOrDefaultAsync<MealOrderPaymentState>(new CommandDefinition(
            sql,
            new { OrderId = orderId.Trim() },
            transaction: transaction,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        await transaction.CommitAsync(cancellationToken);
        return state;
    }

    public async Task MarkPaymentCompletedAsync(
        string orderId,
        string gatewayTransactionId,
        int paymentStatusId,
        int paidOrderStatusId,
        CancellationToken cancellationToken)
    {
        const string updateTransactionSql = """
            UPDATE [Transaction]
            SET StatusId = @StatusId,
                TransactionId = @GatewayTransactionId,
                IsTransactionCompleted = 1,
                UpdatedOn = GETDATE()
            WHERE Id = (SELECT TOP (1) TransactionId FROM [Order] WHERE OrderId = @OrderId);
            """;

        const string updateOrderSql = """
            UPDATE [Order]
            SET OrderStatusId = @OrderStatusId,
                IsPaid = 1,
                UpdatedOn = GETDATE()
            WHERE OrderId = @OrderId;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);

        try
        {
            await dbConnection.ExecuteAsync(new CommandDefinition(
                updateTransactionSql,
                new { OrderId = orderId, GatewayTransactionId = gatewayTransactionId, StatusId = paymentStatusId },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await dbConnection.ExecuteAsync(new CommandDefinition(
                updateOrderSql,
                new { OrderId = orderId, OrderStatusId = paidOrderStatusId },
                transaction: transaction,
                cancellationToken: cancellationToken));

            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task AttachAccessLogIdAsync(
        string orderId,
        long accessLogId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            UPDATE [Transaction]
            SET AccessLogId = @AccessLogId,
                UpdatedOn = GETDATE()
            WHERE Id = (SELECT TOP (1) TransactionId FROM [Order] WHERE OrderId = @OrderId);
            """;

        using var connection = _connectionFactory.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition(
            sql,
            new { OrderId = orderId, AccessLogId = accessLogId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
    }

    public async Task<OrderListResponse> GetOrderListAsync(
        int guardianId,
        int? studentId,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        const string countSql = """
            SELECT COUNT(1)
            FROM [Order] o
            WHERE o.GuardianId = @GuardianId
              AND (@StudentId IS NULL OR o.StudentId = @StudentId);
            """;

        const string dataSql = """
            SELECT
                o.Id,
                o.OrderId,
                o.StudentId,
                o.GuardianId,
                o.Total,
                o.TotalItems,
                o.OrderStatusId,
                IsPaid = ISNULL(o.IsPaid, 0),
                o.OrderDate,
                o.CreatedOn
            FROM [Order] o
            WHERE o.GuardianId = @GuardianId
              AND (@StudentId IS NULL OR o.StudentId = @StudentId)
            ORDER BY o.Id DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var offset = (page - 1) * pageSize;

        var totalCount = await connection.QuerySingleAsync<int>(new CommandDefinition(
            countSql,
            new { GuardianId = guardianId, StudentId = studentId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        var items = (await connection.QueryAsync<OrderListItemDto>(new CommandDefinition(
            dataSql,
            new { GuardianId = guardianId, StudentId = studentId, Offset = offset, PageSize = pageSize },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        return new OrderListResponse
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
            Items = items
        };
    }

    public async Task<OrderDetailDto?> GetOrderDetailByOrderIdAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken)
    {
        const string headerSql = """
            SELECT TOP (1)
                o.Id,
                o.OrderId,
                o.StudentId,
                o.GuardianId,
                o.OrderTypeId,
                o.SubTotal,
                o.TaxAmount,
                o.Total,
                o.TotalItems,
                o.OrderStatusId,
                TransactionStatusId = t.StatusId,
                IsPaid = ISNULL(o.IsPaid, 0),
                IsTransactionCompleted = ISNULL(t.IsTransactionCompleted, 0),
                Notes = ISNULL(o.Notes, ''),
                o.OrderDate,
                o.CreatedOn
            FROM [Order] o
            LEFT JOIN [Transaction] t ON t.Id = o.TransactionId
            WHERE o.GuardianId = @GuardianId
              AND o.OrderId = @OrderId;
            """;

        const string lineItemsSql = """
            SELECT
                oi.Id,
                oi.ItemId,
                oi.PackageId,
                ItemName = ISNULL(COALESCE(mi.ItemName, mp.PackageName), ''),
                oi.ItemPrice,
                oi.MealDate,
                oi.CreatedOn
            FROM [OrderItem] oi
            INNER JOIN [Order] o ON o.Id = oi.OrderId
            LEFT JOIN [MealItem] mi ON mi.Id = oi.ItemId
            LEFT JOIN [MealPackages] mp ON mp.Id = oi.PackageId
            WHERE o.GuardianId = @GuardianId
              AND o.OrderId = @OrderId
            ORDER BY oi.Id ASC;
            """;

        using var connection = _connectionFactory.CreateConnection();

        var order = await connection.QueryFirstOrDefaultAsync<OrderDetailDto>(new CommandDefinition(
            headerSql,
            new { GuardianId = guardianId, OrderId = orderId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (order is null)
        {
            return null;
        }

        var lineItems = (await connection.QueryAsync<OrderDetailLineItemDto>(new CommandDefinition(
            lineItemsSql,
            new { GuardianId = guardianId, OrderId = orderId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        return new OrderDetailDto
        {
            Id = order.Id,
            OrderId = order.OrderId,
            StudentId = order.StudentId,
            GuardianId = order.GuardianId,
            OrderTypeId = order.OrderTypeId,
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            Total = order.Total,
            TotalItems = order.TotalItems,
            OrderStatusId = order.OrderStatusId,
            TransactionStatusId = order.TransactionStatusId,
            IsPaid = order.IsPaid,
            IsTransactionCompleted = order.IsTransactionCompleted,
            Notes = order.Notes,
            OrderDate = order.OrderDate,
            CreatedOn = order.CreatedOn,
            LineItems = lineItems
        };
    }
}
