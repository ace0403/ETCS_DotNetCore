using System.Data.Common;
using Dapper;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Application.Payment;

public sealed class NativePaymentRecord
{
    public string OrderId { get; init; } = string.Empty;
    public string GatewayTransactionId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public int TransactionPkId { get; init; }
    public int GuardianId { get; init; }
    public int StudentId { get; init; }
    public decimal Amount { get; init; }
    public int PaymentMethod { get; init; }
    public int StatusId { get; init; }
    public bool IsPaid { get; init; }
    public bool IsTransactionCompleted { get; init; }
    public long? AccessLogId { get; init; }

    public bool IsNative => PaymentMethod is (int)PaymentMethodEnum.Card
        or (int)PaymentMethodEnum.ApplePay or (int)PaymentMethodEnum.SamsungPay;
    public bool IsCompleted => IsTransactionCompleted && AccessLogId is > 0
        && (Kind == "topup" || IsPaid);

    public bool Matches(int guardianId, int studentId, string? transactionId) =>
        guardianId > 0 && GuardianId == guardianId && studentId > 0 && StudentId == studentId
        && IsNative && !string.IsNullOrWhiteSpace(GatewayTransactionId)
        && (string.IsNullOrWhiteSpace(transactionId)
            || string.Equals(GatewayTransactionId, transactionId.Trim(), StringComparison.Ordinal));
}

public interface INativePaymentRepository
{
    Task<bool> TryMarkCancellationAttemptAsync(NativePaymentVerificationState state, CancellationToken cancellationToken);
    Task SaveCancelledAsync(NativePaymentVerificationState state, ETCS.PaymentGateway.Models.NativePaymentVerificationResult proof,
        CancellationToken cancellationToken);
    Task SaveSessionTermsAsync(NativePaymentVerificationState state, CancellationToken cancellationToken);
    Task SaveDeclinedAsync(NativePaymentVerificationState state, ETCS.PaymentGateway.Models.NativePaymentVerificationResult decline,
        CancellationToken cancellationToken);
    Task<NativePaymentVerificationState?> GetVerificationAsync(NativePaymentRecord payment, CancellationToken cancellationToken);
    Task<bool> TryMarkCaptureAttemptAsync(NativePaymentVerificationState state, CancellationToken cancellationToken);
    Task SaveVerifiedAsync(NativePaymentVerificationState state, ETCS.PaymentGateway.Models.NativePaymentVerificationResult proof,
        CancellationToken cancellationToken);
    Task<NativePaymentRecord?> FindAsync(string orderId, string kind, CancellationToken cancellationToken);
    Task<IAsyncDisposable?> TryAcquireAsync(string orderId, string kind, CancellationToken cancellationToken);
}

public sealed partial class NativePaymentRepository(IMealDbConnectionFactory connections) : INativePaymentRepository
{
    public async Task<NativePaymentRecord?> FindAsync(string orderId, string kind, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(orderId) || kind is not ("order" or "topup")) return null;
        var sql = kind == "order" ? """
            SELECT TOP (1) o.OrderId, 'order' AS Kind, t.Id AS TransactionPkId,
                o.GuardianId, o.StudentId, o.Total AS Amount, t.PaymentMethod, t.StatusId,
                ISNULL(o.IsPaid, 0) AS IsPaid, ISNULL(t.IsTransactionCompleted, 0) AS IsTransactionCompleted,
                t.AccessLogId, LTRIM(RTRIM(t.TransactionId)) AS GatewayTransactionId
            FROM [Order] o INNER JOIN [Transaction] t ON t.Id = o.TransactionId
            WHERE o.OrderId = @OrderId;
            """ : """
            SELECT TOP (1) LTRIM(RTRIM(t.Remarks)) AS OrderId, 'topup' AS Kind,
                t.Id AS TransactionPkId, t.GuardianId, t.StudentId, t.Amount, t.PaymentMethod, t.StatusId,
                CAST(0 AS bit) AS IsPaid, ISNULL(t.IsTransactionCompleted, 0) AS IsTransactionCompleted,
                t.AccessLogId, LTRIM(RTRIM(t.TransactionId)) AS GatewayTransactionId
            FROM [Transaction] t
            WHERE LTRIM(RTRIM(t.Remarks)) = @OrderId
                AND NOT EXISTS (SELECT 1 FROM [Order] o WHERE o.TransactionId = t.Id)
            ORDER BY t.Id DESC;
            """;
        using var connection = connections.CreateConnection();
        return await connection.QueryFirstOrDefaultAsync<NativePaymentRecord>(
            new CommandDefinition(sql, new { OrderId = orderId.Trim() }, cancellationToken: cancellationToken));
    }

    // Session-owned SQL application lock spans gateway and ledger work, including across API instances.
    // No database transaction is held open during network calls.
    public async Task<IAsyncDisposable?> TryAcquireAsync(string orderId, string kind, CancellationToken cancellationToken)
    {
        var connection = (DbConnection)connections.CreateConnection();
        try
        {
            await connection.OpenAsync(cancellationToken);
            var resource = $"ETCS.NativePayment:{kind}:{orderId.Trim()}";
            var result = await connection.ExecuteScalarAsync<int>(new CommandDefinition("""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource=@Resource, @LockMode='Exclusive',
                    @LockOwner='Session', @LockTimeout=0;
                SELECT @result;
                """, new { Resource = resource }, cancellationToken: cancellationToken));
            if (result >= 0) return new Lease(connection, resource);
            await connection.DisposeAsync();
            return null;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    private sealed class Lease(DbConnection connection, string resource) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "EXEC sys.sp_releaseapplock @Resource=@Resource, @LockOwner='Session';",
                    new { Resource = resource }, commandTimeout: 15));
            }
            finally { await connection.DisposeAsync(); }
        }
    }
}
