using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Orders;

public interface IOrderInitiateService
{
    Task<OrderInitiateResponse> InitiateAsync(OrderInitiateRequest request, CancellationToken cancellationToken);
}
