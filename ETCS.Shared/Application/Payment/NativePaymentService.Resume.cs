using ETCS.PaymentGateway.Models;
using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Application.Payment;

public sealed class NativeCardResumeResponse
{
    public bool IsSuccess { get; init; }
    public bool RequiresConfirmation { get; init; }
    public string Message { get; init; } = string.Empty;
    public NativePaymentSessionResult? Session { get; init; }
}

public sealed partial class NativePaymentService
{
    public async Task<NativeCardResumeResponse> ResumeCardAsync(int guardianId, int studentId, string orderId,
        string? transactionId, string kind, string returnUrl, CancellationToken cancellationToken)
    {
        var payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        await using var lease = await payments.TryAcquireAsync(payment.OrderId, kind, cancellationToken);
        if (lease is null) return new() { Message = BusyPayment };
        payment = await ResolveAsync(guardianId, studentId, orderId, transactionId, kind, cancellationToken);
        if (payment is null) return new() { Message = InvalidPayment };
        var state = await payments.GetVerificationAsync(payment, cancellationToken);
        if (state is null || !state.Binds(payment))
            return new() { Message = "This payment needs reconciliation before its card session can be reopened." };
        if (payment.PaymentMethod != (int)PaymentMethodEnum.Card || payment.IsPaid || payment.IsTransactionCompleted
            || state.Captured || state.Declined || state.CaptureAttempted || state.CancellationAttempted || state.Cancelled)
            return new() { IsSuccess = true, RequiresConfirmation = true, Message = "Confirm the existing payment status." };
        if (!IsPending(payment)) return new() { Message = "Payment is not pending." };
        var session = await gateway.ResumeNativeCardSessionAsync(state.ToRequest(), returnUrl, cancellationToken);
        // If the gateway no longer serves the page, preserve the original reference and
        // let the existing verifier reconcile it. Never replace it with a new registration.
        if (!session.IsSuccess)
            return new() { IsSuccess = true, RequiresConfirmation = true, Message = session.Message };
        return new() { IsSuccess = true, Session = session, Message = session.Message };
    }
}
