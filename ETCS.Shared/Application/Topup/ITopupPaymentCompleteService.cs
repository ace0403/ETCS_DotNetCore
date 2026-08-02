namespace ETCS.Shared.Application.Topup;

public interface ITopupPaymentCompleteService
{
    Task<TopupCompleteResponse> CompleteAsync(TopupCompleteRequest request, CancellationToken cancellationToken);
}
