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
}
