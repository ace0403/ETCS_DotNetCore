using System.Text.Json;
using Microsoft.Extensions.Logging;
using ETCS.PaymentGateway.Models;

namespace ETCS.Shared.Application.Payment;

public sealed class NativePaymentCancelResponse
{
    public bool IsSuccess { get; init; }
    public bool CanStartNew { get; init; }
    public bool RequiresCompletion { get; init; }
    public bool IsPending { get; init; }
    public string Status { get; init; } = "pending";
    public string Message { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string Type { get; init; } = string.Empty;
}

public sealed partial class NativePaymentService
{
    private static NativePaymentVerificationResult? ReadCancellationProof(NativePaymentVerificationState state)
    {
        if (!state.Cancelled || state.Captured || state.CaptureAttempted || state.Declined) return null;
        try
        {
            var proof = JsonSerializer.Deserialize<NativePaymentVerificationResult>(state.CancellationProofJson ?? "");
            return proof?.MatchesCancellation(state.ToRequest()) == true && (proof.ResponseCode == "6824" || state.CancellationAttempted) ? proof : null;
        }
        catch (JsonException) { return null; }
    }

    public async Task<NativePaymentCancelResponse> CancelAsync(int guardianId, int studentId, string orderId,
        string transactionId, string kind, CancellationToken cancellationToken, bool verifyOnly = false)
    {
        // Never treat a missing/mismatched reference as permission to replace a payment.
        if (string.IsNullOrWhiteSpace(transactionId) || kind is not ("order" or "topup")) return new() { Message = InvalidPayment };
        var payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        NativePaymentCancelResponse Reply(string message, string status = "pending", bool release = false, bool complete = false) => new()
        {
            IsSuccess = true, CanStartNew = release, RequiresCompletion = complete, IsPending = !release && !complete,
            Message = message, Status = status, OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId,
            StudentId = payment.StudentId, Type = payment.Kind
        };
        await using var lease = await payments.TryAcquireAsync(payment.OrderId, kind, cancellationToken);
        if (lease is null) return Reply(BusyPayment);
        payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        var state = await payments.GetVerificationAsync(payment, cancellationToken);
        if (state is null || !state.Binds(payment)) return Reply("The original payment needs reconciliation before it can be replaced.");
        if (state.Captured)
            return Reply("The original payment may already be paid. Confirm its completion instead.", "requires-completion", complete: true);
        if (payment.IsPaid || payment.IsTransactionCompleted)
            return Reply("The recorded payment status needs captured-payment evidence before this request can be replaced.");
        if (state.Cancelled)
        {
            var storedCancellation = ReadCancellationProof(state);
            if (storedCancellation is null) return Reply("Stored cancellation evidence needs reconciliation.");
            // Repair history rows from older API versions using the already-validated proof.
            using var save = timeouts.CreateDbTimeoutSource();
            await payments.SaveCancelledAsync(state, storedCancellation, save.Token);
            return Reply("The original payment was cancelled. You can start a new request.", "cancelled", release: true);
        }
        if (state.Declined)
            return !state.CaptureAttempted && state.DeclineCode is ("5" or "51")
                ? Reply("The original payment was declined. You can start a new request.", "declined", release: true)
                : Reply("Stored decline evidence needs reconciliation.");
        if (!IsPending(payment)) return Reply("The original payment is not in a cancellable pending state.");

        // Verification only: deliberately do not call VerifyCapturedAsync, which can Capture.
        var result = await gateway.FinalizeNativePaymentAsync(state.ToRequest(), cancellationToken);
        // A close callback may leave an unpaid SDK page active. Explicitly close it,
        // then demand fresh Finalization evidence. Never do this after uncertain financial work,
        // with invalid metadata, or when Finalization did not return a provider response code.
        if (!verifyOnly && !state.CaptureAttempted && !state.CancellationAttempted && !result.IsSuccess
            && !result.IsAuthorized && !result.IsCaptured && !result.IsCancelled
            && result.Status is ("pending" or "failed" or "awaiting-sdk")
            && result.ResponseCode is { Length: > 0 and <= 12 } closeCode && closeCode != "0" && closeCode.All(char.IsAsciiDigit))
            result = await gateway.CancelNativeSdkPaymentAsync(state.ToRequest(), cancellationToken);
        if (result.Matches(state.ToRequest()))
        {
            using var save = timeouts.CreateDbTimeoutSource();
            await payments.SaveVerifiedAsync(state, result, save.Token);
            return Reply("The original payment is paid. Complete it instead of paying again.", "requires-completion", complete: true);
        }
        if (state.CaptureAttempted) return Reply("An earlier capture attempt is unconfirmed. Keep this payment for reconciliation.");
        if (result.IsCancelled && result.MatchesCancellation(state.ToRequest()))
        {
            using var save = timeouts.CreateDbTimeoutSource();
            await payments.SaveCancelledAsync(state, result, save.Token);
            return Reply("The gateway confirmed the original payment was cancelled. You can start a new request.", "cancelled", release: true);
        }
        if (result.Status == "declined" && !result.IsSuccess && !result.IsPending && !result.IsCaptured
            && result.ResponseCode is ("5" or "51") && result.TransactionId == state.GatewayTransactionId && result.OrderId == state.OrderId)
        {
            using var save = timeouts.CreateDbTimeoutSource();
            await payments.SaveDeclinedAsync(state, result, save.Token);
            return Reply("The original payment was declined. You can start a new request.", "declined", release: true);
        }
        if (state.CancellationAttempted)
            return Reply("A cancellation was already requested but is unconfirmed. Reconciliation is required; it will not be sent again.");
        if (!state.ManualCapture || !result.IsAuthorized)
        {
            var safeCode = result.ResponseCode is { Length: > 0 and <= 12 } code && code.All(char.IsAsciiDigit) ? code : "unavailable";
            logger.LogWarning("Native cancellation remains unconfirmed. GatewayStatus={GatewayStatus}; ResponseCode={ResponseCode}; PaymentMethod={PaymentMethod}; ManualCapture={ManualCapture}; CaptureAttempted={CaptureAttempted}; CancellationAttempted={CancellationAttempted}",
                result.Status, safeCode, payment.PaymentMethod, state.ManualCapture, state.CaptureAttempted, state.CancellationAttempted);
            if (result.Status == "awaiting-sdk")
                return Reply("The gateway has no completed payment result. Resume the original payment before trying again.", "awaiting-sdk");
            return Reply("The gateway has not confirmed that this payment is closed. Resume or confirm the original payment before starting another.");
        }
        if (verifyOnly)
            return Reply("The original payment is still authorized. Use Cancel payment to request cancellation.");
        // The attempt is durable BEFORE Reversal. A lost response must never cause another reversal.
        if (!await payments.TryMarkCancellationAttemptAsync(state, cancellationToken)) return Reply(BusyPayment);
        var reversed = await gateway.ReverseNativePaymentAsync(state.ToRequest(), result, cancellationToken);
        if (!reversed.MatchesCancellation(state.ToRequest()))
            return Reply("Cancellation is unconfirmed. Keep the original reference and contact support if confirmation remains unavailable.");
        using var saveCancellation = timeouts.CreateDbTimeoutSource();
        await payments.SaveCancelledAsync(state, reversed, saveCancellation.Token);
        return Reply("The original payment was cancelled. You can start a new request.", "cancelled", release: true);
    }
}
