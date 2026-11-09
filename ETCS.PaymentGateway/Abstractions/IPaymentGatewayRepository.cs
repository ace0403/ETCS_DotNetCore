using ETCS.PaymentGateway.Models;

namespace ETCS.PaymentGateway.Abstractions;

public interface IPaymentGatewayRepository
{
    Task<PaymentSessionCreateResult> CreateTopupSessionAsync(
        StudentTopupPaymentRequest request,
        string orderId,
        CancellationToken cancellationToken,
        string? returnUrl = null);

    Task<PaymentSessionCreateResult> CreateOrderSessionAsync(
        OrderPaymentSessionRequest request,
        CancellationToken cancellationToken);

    Task<PaymentCaptureResult> CapturePaymentAsync(
        PaymentCaptureRequest request,
        CancellationToken cancellationToken);

    Task<NativePaymentVerificationResult> FinalizeNativePaymentAsync(
        NativePaymentVerificationRequest request, CancellationToken cancellationToken);

    Task<NativePaymentVerificationResult> CaptureNativePaymentAsync(
        NativePaymentVerificationRequest request, NativePaymentVerificationResult authorization,
        CancellationToken cancellationToken);

    Task<NativePaymentSessionResult> ResumeNativeCardSessionAsync(NativePaymentVerificationRequest expected,
        string returnUrl, CancellationToken cancellationToken);

    Task<NativePaymentVerificationResult> ReverseNativePaymentAsync(NativePaymentVerificationRequest expected,
        NativePaymentVerificationResult authorization, CancellationToken cancellationToken);

    Task<NativePaymentVerificationResult> CancelNativeSdkPaymentAsync(
        NativePaymentVerificationRequest expected, CancellationToken cancellationToken);

    Task<GenerateTokenResult> GenerateTokenAsync(CancellationToken cancellationToken);

    Task<NativePaymentSessionResult> CreateNativeTopupSessionAsync(
        StudentTopupPaymentRequest request,
        string orderId,
        CancellationToken cancellationToken,
        string? returnUrl = null);

    Task<NativePaymentSessionResult> CreateNativeOrderSessionAsync(
        OrderPaymentSessionRequest request,
        CancellationToken cancellationToken);

    Task<NativeWalletSessionResult> CreateWalletRegistrationAsync(
        WalletRegistrationRequest request,
        CancellationToken cancellationToken);
}
