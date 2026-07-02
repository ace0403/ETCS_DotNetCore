using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Orders;

public interface IOrderPaymentCompleteService
{
    Task<OrderCompleteResponse> CompleteAsync(OrderCompleteRequest request, CancellationToken cancellationToken);
}
