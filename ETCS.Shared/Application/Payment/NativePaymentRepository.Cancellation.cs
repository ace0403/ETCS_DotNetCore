using Dapper;
using System.Text.Json;
using ETCS.PaymentGateway.Models;

namespace ETCS.Shared.Application.Payment;

public sealed partial class NativePaymentRepository
{
    public async Task<bool> TryMarkCancellationAttemptAsync(NativePaymentVerificationState state, CancellationToken cancellationToken)
    {
        using var connection = connections.CreateConnection();
        return await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.NativePaymentVerification
            SET CancellationAttemptedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
            WHERE TransactionPkId = @TransactionPkId AND OrderId = @OrderId AND Kind = @Kind
                AND GatewayTransactionId = @GatewayTransactionId AND Amount = @Amount AND Currency = @Currency
                AND ManualCapture = 1 AND CancellationAttemptedAt IS NULL AND CancelledAt IS NULL
                AND CaptureAttemptedAt IS NULL AND CapturedAt IS NULL AND DeclinedAt IS NULL;
            """, state, cancellationToken: cancellationToken)) == 1;
    }

    public async Task SaveCancelledAsync(NativePaymentVerificationState state, NativePaymentVerificationResult proof,
        CancellationToken cancellationToken)
    {
        if (state.Captured || state.CaptureAttempted || state.Declined || !proof.MatchesCancellation(state.ToRequest()))
            throw new InvalidOperationException("Confirmed native cancellation evidence is required.");
        using var connection = connections.CreateConnection();
        var db = (System.Data.Common.DbConnection)connection;
        await db.OpenAsync(cancellationToken);
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        try
        {
            var parameters = new { state.TransactionPkId, state.OrderId, state.Kind, state.GatewayTransactionId,
                state.Amount, state.Currency, state.ManualCapture, PayerCancelled = proof.ResponseCode == "6824",
                Proof = JsonSerializer.Serialize(proof), CancelledStatus = (int)ETCS.Shared.Enumeration.TransactionStatusEnum.Cancelled,
                PendingStatus = (int)ETCS.Shared.Enumeration.TransactionStatusEnum.Pending,
                InitiatedStatus = (int)ETCS.Shared.Enumeration.TransactionStatusEnum.Initiated,
                Card = (int)ETCS.Shared.Enumeration.PaymentMethodEnum.Card,
                ApplePay = (int)ETCS.Shared.Enumeration.PaymentMethodEnum.ApplePay,
                SamsungPay = (int)ETCS.Shared.Enumeration.PaymentMethodEnum.SamsungPay };
            var count = await db.ExecuteAsync(new CommandDefinition("""
                UPDATE dbo.NativePaymentVerification
                SET CancelledAt = COALESCE(CancelledAt, SYSUTCDATETIME()), CancellationProofJson = @Proof,
                    UpdatedAt = SYSUTCDATETIME()
                WHERE TransactionPkId = @TransactionPkId AND OrderId = @OrderId AND Kind = @Kind
                    AND GatewayTransactionId = @GatewayTransactionId AND Amount = @Amount AND Currency = @Currency
                    AND ManualCapture = @ManualCapture AND (@PayerCancelled = 1 OR (ManualCapture = 1 AND CancellationAttemptedAt IS NOT NULL))
                    AND CaptureAttemptedAt IS NULL AND CapturedAt IS NULL AND DeclinedAt IS NULL;
                """, parameters, transaction: transaction, cancellationToken: cancellationToken));
            if (count != 1) throw new InvalidOperationException("Native cancellation evidence could not be persisted.");
            count = await db.ExecuteAsync(new CommandDefinition("""
                UPDATE [Transaction]
                SET StatusId = @CancelledStatus, UpdatedOn = GETDATE()
                WHERE Id = @TransactionPkId AND LTRIM(RTRIM(TransactionId)) = @GatewayTransactionId
                    AND Amount = @Amount AND PaymentMethod IN (@Card, @ApplePay, @SamsungPay)
                    AND StatusId IN (@PendingStatus, @InitiatedStatus, @CancelledStatus)
                    AND ISNULL(IsTransactionCompleted, 0) = 0 AND ISNULL(AccessLogId, 0) = 0
                    AND ((@Kind = 'order' AND EXISTS (SELECT 1 FROM [Order] o WHERE o.TransactionId = [Transaction].Id
                            AND o.OrderId = @OrderId AND o.Total = @Amount AND ISNULL(o.IsPaid, 0) = 0))
                        OR (@Kind = 'topup' AND LTRIM(RTRIM(Remarks)) = @OrderId
                            AND NOT EXISTS (SELECT 1 FROM [Order] o WHERE o.TransactionId = [Transaction].Id)));
                """, parameters, transaction: transaction, cancellationToken: cancellationToken));
            if (count != 1) throw new InvalidOperationException("Original native transaction could not be marked cancelled.");
            if (state.Kind == "order")
            {
                count = await db.ExecuteAsync(new CommandDefinition("""
                    UPDATE [Order] SET OrderStatusId = @CancelledStatus, UpdatedOn = GETDATE()
                    WHERE OrderId = @OrderId AND TransactionId = @TransactionPkId AND Total = @Amount
                        AND ISNULL(IsPaid, 0) = 0 AND OrderStatusId IN (@PendingStatus, @InitiatedStatus, @CancelledStatus);
                    """, parameters, transaction: transaction, cancellationToken: cancellationToken));
                if (count != 1) throw new InvalidOperationException("Original native order could not be marked cancelled.");
            }
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
