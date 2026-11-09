using Dapper;
using ETCS.PaymentGateway.Models;

namespace ETCS.Shared.Application.Payment;

public sealed partial class NativePaymentRepository
{
    public async Task SaveDeclinedAsync(NativePaymentVerificationState state,
        NativePaymentVerificationResult decline, CancellationToken cancellationToken)
    {
        if (state.Cancelled || state.CaptureAttempted || decline.IsSuccess || decline.IsPending || decline.IsCaptured
            || decline.Status != "declined" || decline.ResponseCode is not ("5" or "51")
            || decline.OrderId != state.OrderId || decline.TransactionId != state.GatewayTransactionId)
            throw new InvalidOperationException("Definitive native decline evidence is required.");
        using var connection = connections.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.NativePaymentVerification
            SET DeclinedAt = COALESCE(DeclinedAt, SYSUTCDATETIME()), DeclineCode = @ResponseCode,
                UpdatedAt = SYSUTCDATETIME()
            WHERE TransactionPkId = @TransactionPkId AND GatewayTransactionId = @GatewayTransactionId
                AND OrderId = @OrderId AND Amount = @Amount AND Currency = @Currency
                AND CaptureAttemptedAt IS NULL AND CapturedAt IS NULL AND CancelledAt IS NULL;
            """, new { state.TransactionPkId, state.GatewayTransactionId, state.OrderId, state.Amount, state.Currency,
                decline.ResponseCode }, cancellationToken: cancellationToken));
        if (updated != 1) throw new InvalidOperationException("Native decline could not be persisted.");
    }
}
