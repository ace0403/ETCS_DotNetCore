using ETCS.Shared.Infrastructure.Pos;

namespace ETCS.Shared.Application.Topup;

public interface IManualTopupService
{
    Task<PosCardCheckResponse> CheckCardAsync(
        string cardNumber,
        CancellationToken cancellationToken);

    Task<PosManualTopupResponse> ProcessAsync(
        PosManualTopupRequest request,
        CancellationToken cancellationToken);
}
