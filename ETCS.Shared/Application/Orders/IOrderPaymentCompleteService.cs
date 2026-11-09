using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Orders;

public interface IOrderPaymentCompleteService
{
    Task<OrderCompleteResponse> CompleteVerifiedNativeAsync(OrderCompleteRequest request,
        ETCS.PaymentGateway.Models.NativePaymentVerificationResult proof, CancellationToken cancellationToken);

    Task<OrderCompleteResponse> CompleteAsync(OrderCompleteRequest request, CancellationToken cancellationToken);
}
