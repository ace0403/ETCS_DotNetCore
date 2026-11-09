using System.Text.Json;
using Dapper;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Application.Payment;

public sealed class NativePaymentVerificationState
{
    public int TransactionPkId { get; init; }
    public string OrderId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string GatewayTransactionId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public bool ManualCapture { get; init; }
    public bool CaptureAttempted { get; init; }
    public bool Captured { get; init; }
    public bool Declined { get; init; }
    public bool CancellationAttempted { get; init; }
    public bool Cancelled { get; init; }
    public string? CancellationProofJson { get; init; }
    public string? DeclineCode { get; init; }
    public string? ProofJson { get; init; }

    public NativePaymentVerificationRequest ToRequest() =>
        new(GatewayTransactionId, OrderId, Amount, Currency, ManualCapture);

    public bool Binds(NativePaymentRecord payment) =>
        payment.TransactionPkId == TransactionPkId && payment.OrderId == OrderId && payment.Kind == Kind
        && payment.GatewayTransactionId == GatewayTransactionId && payment.Kind == Kind
        && payment.Amount == Amount;
}

public sealed partial class NativePaymentRepository
{
    public async Task SaveSessionTermsAsync(NativePaymentVerificationState state, CancellationToken cancellationToken)
    {
        // Immutable registration terms are saved before the app receives the SDK session.
        using var connection = connections.CreateConnection();
        await connection.ExecuteAsync(new CommandDefinition("""
            INSERT dbo.NativePaymentVerification
                (TransactionPkId, OrderId, Kind, GatewayTransactionId, Amount, Currency, ManualCapture)
            VALUES (@TransactionPkId, @OrderId, @Kind, @GatewayTransactionId, @Amount, @Currency, @ManualCapture);
            """, state, cancellationToken: cancellationToken));
    }

    public async Task<NativePaymentVerificationState?> GetVerificationAsync(
        NativePaymentRecord payment, CancellationToken cancellationToken)
    {
        using var connection = connections.CreateConnection();
        return await connection.QuerySingleOrDefaultAsync<NativePaymentVerificationState>(new CommandDefinition("""
            SELECT TransactionPkId, OrderId, Kind, GatewayTransactionId, Amount, Currency, ManualCapture,
                CAST(CASE WHEN CaptureAttemptedAt IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CaptureAttempted,
                CAST(CASE WHEN CapturedAt IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Captured,
                ProofJson, DeclineCode, CancellationProofJson,
                CAST(CASE WHEN CancellationAttemptedAt IS NOT NULL THEN 1 ELSE 0 END AS bit) AS CancellationAttempted,
                CAST(CASE WHEN CancelledAt IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Cancelled,
                CAST(CASE WHEN DeclinedAt IS NOT NULL THEN 1 ELSE 0 END AS bit) AS Declined
            FROM dbo.NativePaymentVerification WHERE TransactionPkId = @TransactionPkId;
            """, payment, cancellationToken: cancellationToken));
    }

    public async Task<bool> TryMarkCaptureAttemptAsync(
        NativePaymentVerificationState state, CancellationToken cancellationToken)
    {
        using var connection = connections.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.NativePaymentVerification
            SET CaptureAttemptedAt = SYSUTCDATETIME(), UpdatedAt = SYSUTCDATETIME()
            WHERE TransactionPkId = @TransactionPkId AND GatewayTransactionId = @GatewayTransactionId
                AND CaptureAttemptedAt IS NULL AND CapturedAt IS NULL AND DeclinedAt IS NULL
                AND CancellationAttemptedAt IS NULL AND CancelledAt IS NULL;
            """, state, cancellationToken: cancellationToken));
        return updated == 1;
    }

    public async Task SaveVerifiedAsync(NativePaymentVerificationState state,
        NativePaymentVerificationResult proof, CancellationToken cancellationToken)
    {
        if (!proof.Matches(state.ToRequest())) throw new InvalidOperationException("Invalid native capture proof.");
        using var connection = connections.CreateConnection();
        var updated = await connection.ExecuteAsync(new CommandDefinition("""
            UPDATE dbo.NativePaymentVerification
            SET CapturedAt = COALESCE(CapturedAt, SYSUTCDATETIME()),
                ProofJson = @ProofJson, UpdatedAt = SYSUTCDATETIME()
            WHERE TransactionPkId = @TransactionPkId AND GatewayTransactionId = @GatewayTransactionId
                AND OrderId = @OrderId AND Amount = @Amount AND Currency = @Currency AND DeclinedAt IS NULL AND CancelledAt IS NULL;
            """, new { state.TransactionPkId, state.GatewayTransactionId, state.OrderId, state.Amount, state.Currency,
                ProofJson = JsonSerializer.Serialize(proof) }, cancellationToken: cancellationToken));
        if (updated != 1) throw new InvalidOperationException("Native capture proof could not be persisted.");
    }
}
