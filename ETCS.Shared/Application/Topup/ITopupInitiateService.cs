namespace ETCS.Shared.Application.Topup;

public interface ITopupInitiateService
{
    Task<TopupInitiateResponse> InitiateAsync(TopupInitiateRequest request, CancellationToken cancellationToken);
}
