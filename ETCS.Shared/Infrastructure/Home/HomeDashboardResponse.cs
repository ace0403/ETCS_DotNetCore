using ETCS.Shared.Application.History;

namespace ETCS.Shared.Infrastructure.Home;

public sealed class HomeDashboardResponse
{
    public int GuardianId { get; init; }

    public IReadOnlyList<GuardianHistoryListItemDto> RecentTransactions { get; init; } = [];
}
