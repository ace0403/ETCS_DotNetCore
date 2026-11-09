using System.Globalization;
using System.Text.Json;
using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Payment;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Application.Payment;

public interface INativePaymentService
{
    Task<NativePaymentCancelResponse> CancelAsync(int guardianId, int studentId, string orderId,
        string transactionId, string kind, CancellationToken cancellationToken, bool verifyOnly = false);
    Task<NativeCardResumeResponse> ResumeCardAsync(int guardianId, int studentId, string orderId,
        string? transactionId, string kind, string returnUrl, CancellationToken cancellationToken);
    Task<PaymentStatusResponse> GetStatusAsync(int guardianId, int studentId, string orderId,
        string? transactionId, string kind, CancellationToken cancellationToken);
    Task<TopupCompleteResponse> CompleteTopupAsync(int guardianId, int studentId, string orderId,
        string? transactionId, CancellationToken cancellationToken);
    Task<OrderCompleteResponse> CompleteOrderAsync(int guardianId, int studentId, string orderId,
        string? transactionId, CancellationToken cancellationToken);
}

public sealed partial class NativePaymentService(
    INativePaymentRepository payments,
    IPaymentGatewayRepository gateway,
    IOrderPaymentCompleteService orders,
    ITransactionRepository transactions,
    IMainOrderRepository ledger,
    IStudentRepository students,
    IGuardianEmailNotificationService emails,
    IGuardianInAppNotificationService notifications,
    IPaymentBackgroundQueue logs,
    PaymentCompletionCancellation timeouts,
    ILogger<NativePaymentService> logger) : INativePaymentService
{
    private const string InvalidPayment = "Payment not found or payment references do not match.";
    private const string BusyPayment = "Payment confirmation is still processing. Please retry.";

    private async Task<NativePaymentRecord?> ResolveAsync(int guardianId, int studentId, string orderId,
        string? transactionId, string kind, CancellationToken cancellationToken)
    {
        var payment = await payments.FindAsync(orderId, kind, cancellationToken);
        if (payment?.Matches(guardianId, studentId, transactionId) != true) return null;
        var guardian = await students.GetGuardianBasicDetailByStudentIdAsync(
            studentId.ToString(CultureInfo.InvariantCulture), cancellationToken);
        return guardian?.GuardianId == guardianId ? payment : null;
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(int guardianId, int studentId, string orderId,
        string? transactionId, string kind, CancellationToken cancellationToken)
    {
        var payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        await using var lease = await payments.TryAcquireAsync(payment.OrderId, kind, cancellationToken);
        if (lease is null) return new() { IsPending = true, Message = BusyPayment };
        payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        var result = await VerifyCapturedAsync(payment, cancellationToken);
        return new()
        {
            IsSuccess = result.IsSuccess, IsPending = result.IsPending,
            IsAlreadyProcessed = result.IsSuccess && payment.IsCompleted,
            Message = result.Message, Status = result.Status, OrderId = payment.OrderId,
            TransactionId = payment.GatewayTransactionId
        };
    }
    public async Task<OrderCompleteResponse> CompleteOrderAsync(int guardianId, int studentId, string orderId,
        string? transactionId, CancellationToken cancellationToken)
    {
        var payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, "order", cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        await using var lease = await payments.TryAcquireAsync(payment.OrderId, "order", cancellationToken);
        if (lease is null) return new() { IsPending = true, Message = BusyPayment };
        payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, "order", cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        var proof = await VerifyCapturedAsync(payment, cancellationToken);
        if (!proof.IsSuccess || proof.IsPending)
            return new() { IsPending = proof.IsPending, Message = proof.Message,
                OrderId = payment.OrderId, GatewayTransactionId = payment.GatewayTransactionId };
        return await orders.CompleteVerifiedNativeAsync(new OrderCompleteRequest
        {
            GuardianId = payment.GuardianId, StudentId = payment.StudentId,
            OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId, UseNativeCredentials = true
        }, proof, cancellationToken);
    }

    public async Task<TopupCompleteResponse> CompleteTopupAsync(int guardianId, int studentId, string orderId,
        string? transactionId, CancellationToken cancellationToken)
    {
        var payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, "topup", cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        await using var lease = await payments.TryAcquireAsync(payment.OrderId, "topup", cancellationToken);
        if (lease is null) return new() { IsPending = true, Message = BusyPayment };
        payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, "topup", cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        var proof = await VerifyCapturedAsync(payment, cancellationToken);
        if (!proof.IsSuccess || proof.IsPending)
            return new() { IsPending = proof.IsPending, Message = proof.Message,
                OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId, Status = proof.Status };
        if (payment.IsCompleted) return TopupResult(payment, success: true, alreadyProcessed: true);
        // Once captured, finish independently of a disconnected phone. Leave the MealDB row retryable
        // until the transactional wallet procedure has produced its ledger record.
        using var dbTimeout = timeouts.CreateDbTimeoutSource();
        var dbToken = dbTimeout.Token;
        try
        {
            var guardian = await students.GetGuardianBasicDetailByStudentIdAsync(
                payment.StudentId.ToString(CultureInfo.InvariantCulture), dbToken);
            if (guardian is null || string.IsNullOrWhiteSpace(guardian.CustomerId))
                return TopupResult(payment, message: "Payment captured; customer details are unavailable. Please retry.");

            // This SP transaction owns BOTH crediting the wallet and inserting AccessLog.
            // Its transaction-id guard makes replay after a process crash safe. Never fabricate an AccessLog here.
            await transactions.UpdatePendingAndTopupTransactionAsync(
                new UpdatePendingTransactionRequest { CustomerID = guardian.CustomerId, Loaded = "1",
                    Creby = guardian.Email, PaymentDetails = payment.GatewayTransactionId, Remarks = payment.OrderId },
                new UpdateTopupTransactionRequest { CustomerID = guardian.CustomerId, Remarks = payment.OrderId }, dbToken);
            var accessLogId = await ledger.FindAccessLogIdByGatewayTransactionAsync(
                guardian.CustomerId, payment.GatewayTransactionId, dbToken);
            if (accessLogId is not > 0)
                return TopupResult(payment, message: "Payment captured; wallet credit is still processing. Please retry.");

            await transactions.AttachAccessLogIdByTransactionPkAsync(payment.TransactionPkId, accessLogId.Value, dbToken);
            await transactions.UpdateTopupTransactionStatusAsync(new TopupTransactionUpdateRequest
            {
                TransactionPkId = payment.TransactionPkId, GatewayTransactionId = payment.GatewayTransactionId,
                StatusId = (int)TransactionStatusEnum.Success, IsTransactionCompleted = true,
                Remarks = payment.OrderId, UpdatedBy = payment.GuardianId
            }, dbToken);

            try
            {
                if (!string.IsNullOrWhiteSpace(guardian.Email))
                    await emails.QueueTopupSuccessAsync(payment.StudentId, payment.GuardianId, guardian.Email,
                        guardian.GuardianName, payment.OrderId, payment.GatewayTransactionId, payment.Amount, dbToken);
                var schoolId = await students.GetStudentSchoolIdAsync(payment.StudentId, dbToken);
                await notifications.CreateTopupSuccessAsync(payment.StudentId, payment.GuardianId, payment.Amount,
                    payment.OrderId, schoolId, dbToken);
            }
            catch (Exception ex) { logger.LogWarning(ex, "Native top-up notification failed for {OrderId}", payment.OrderId); }
            return TopupResult(payment, success: true);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Native top-up wallet completion will retry for {OrderId}", payment.OrderId);
            return TopupResult(payment, message: "Payment captured; wallet credit could not be confirmed. Please retry.");
        }
    }

    private async Task<NativePaymentVerificationResult> VerifyCapturedAsync(
        NativePaymentRecord payment, CancellationToken cancellationToken)
    {
        var state = await payments.GetVerificationAsync(payment, cancellationToken);
        if (state is null)
            return PendingVerification("This older native payment needs gateway reconciliation before completion.", payment);
        if (!state.Binds(payment))
            return new() { Message = "Stored registration terms do not match the payment.", Status = "invalid-response" };
        var expected = state.ToRequest();
        if (state.Cancelled)
        {
            var cancellation = !payment.IsPaid && !payment.IsTransactionCompleted ? ReadCancellationProof(state) : null;
            if (cancellation is null) return PendingVerification("Stored cancellation evidence needs reconciliation.", payment);
            using var saveCancellation = timeouts.CreateDbTimeoutSource();
            await payments.SaveCancelledAsync(state, cancellation, saveCancellation.Token);
            return new() { Status = "cancelled", Message = "The original payment was cancelled.",
                OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId };
        }
        if (state.Declined)
            return !state.Captured && !state.CaptureAttempted && state.DeclineCode is ("5" or "51") ? new()
            {
                Status = "declined", ResponseCode = state.DeclineCode,
                Message = "Payment was declined by the bank. Please choose another payment method.",
                OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId
            } : new() { Status = "invalid-response", Message = "Stored decline evidence requires reconciliation." };
        if (state.Captured)
        {
            try
            {
                var saved = JsonSerializer.Deserialize<NativePaymentVerificationResult>(state.ProofJson ?? string.Empty);
                if (saved?.Matches(expected) == true) return saved;
            }
            catch (JsonException) { }
            return new() { Message = "Stored capture evidence is invalid. Reconciliation is required.", Status = "invalid-response" };
        }
        if (!IsPending(payment) && !payment.IsPaid && !payment.IsTransactionCompleted)
            return new() { Message = "Payment is not pending." };

        var result = await gateway.FinalizeNativePaymentAsync(expected, cancellationToken);
        if (state.CaptureAttempted && (result.Status == "declined" || result.IsCancelled))
            return PendingVerification("An earlier capture attempt needs gateway reconciliation before this payment can be released.", payment);
        if (result.IsCancelled)
        {
            if (!result.MatchesCancellation(expected)) return PendingVerification("Gateway cancellation evidence needs reconciliation.", payment);
            using var saveCancellation = timeouts.CreateDbTimeoutSource();
            await payments.SaveCancelledAsync(state, result, saveCancellation.Token);
            return result;
        }
        if (result.IsAuthorized && !result.IsCaptured)
        {
            if (state.CancellationAttempted)
                return PendingVerification("An earlier cancellation attempt needs reconciliation. No capture will be sent.", payment);
            // The attempt is committed BEFORE sending money-moving Capture. After any uncertain
            // outcome, retry Finalization to recover captured evidence; never blindly issue Capture again.
            if (state.CaptureAttempted || !await payments.TryMarkCaptureAttemptAsync(state, cancellationToken))
                return PendingVerification("Capture was already requested. Gateway reconciliation is required before retrying capture.", payment);
            result = await gateway.CaptureNativePaymentAsync(expected, result, cancellationToken);
        }
        logs.EnqueuePaymentLog(payment.OrderId, JsonSerializer.Serialize(result));
        if (result.Status == "declined")
        {
            // Save the terminal decision before allowing the app to start another payment.
            // Background reconciliation must never finalize this declined transaction again.
            using var declineTimeout = timeouts.CreateDbTimeoutSource();
            await payments.SaveDeclinedAsync(state, result, declineTimeout.Token);
            return result;
        }
        if (!result.IsSuccess || result.IsPending) return result;
        if (!result.Matches(expected))
            return new() { Message = "Gateway capture evidence does not match the registered payment.", Status = "invalid-response" };

        // Persist successful capture independently of the disconnected phone before any ledger work.
        using var dbTimeout = timeouts.CreateDbTimeoutSource();
        await payments.SaveVerifiedAsync(state, result, dbTimeout.Token);
        return result;
    }

    private static NativePaymentVerificationResult PendingVerification(string message, NativePaymentRecord payment) => new()
    {
        IsPending = true, Message = message, Status = "pending", OrderId = payment.OrderId,
        TransactionId = payment.GatewayTransactionId
    };
    private static bool IsPending(NativePaymentRecord payment) =>
        payment.StatusId is (int)TransactionStatusEnum.Initiated or (int)TransactionStatusEnum.Pending;

    private static TopupCompleteResponse TopupResult(NativePaymentRecord payment, bool success = false,
        bool alreadyProcessed = false, string? message = null) => new()
    {
        IsSuccess = success, IsAlreadyProcessed = alreadyProcessed, IsPending = !success,
        Message = message ?? (alreadyProcessed ? "Top-up already completed." : "Top-up completed."),
        OrderId = payment.OrderId, TransactionId = payment.GatewayTransactionId, Amount = payment.Amount,
        Status = success ? "completed" : "pending"
    };
}
