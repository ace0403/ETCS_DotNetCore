using ETCS.Shared.Infrastructure.Pos;

namespace ETCS.Shared.Application.Pos;

public interface IPosOrderInitiateService
{
    Task<PosOrderInitiateResponse> InitiateAsync(PosOrderInitiateRequest request, CancellationToken cancellationToken);
}
