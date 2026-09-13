using ETCS.Shared.Infrastructure.Pos;

namespace ETCS.Shared.Application.Pos;

public interface IPosNfcPurchaseService
{
    Task<PosNfcPurchaseResponse> PurchaseAsync(
        PosNfcPurchaseRequest request,
        CancellationToken cancellationToken);

    Task<PosNfcPurchaseResponse> UndoAsync(
        PosNfcUndoRequest request,
        CancellationToken cancellationToken);
}
