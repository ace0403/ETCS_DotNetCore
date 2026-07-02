using ETCS.Shared.Infrastructure.Pos;

namespace ETCS.Shared.Application.Pos;

public interface IPosOrderCompleteService
{
    Task<PosOrderCompleteResponse> CompleteAsync(PosOrderCompleteRequest request, CancellationToken cancellationToken);

    Task<PosOrderCompleteResponse> UndoAsync(PosOrderUndoRequest request, CancellationToken cancellationToken);
}
