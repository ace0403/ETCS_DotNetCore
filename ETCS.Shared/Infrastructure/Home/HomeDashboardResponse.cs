using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Infrastructure.Home;

public sealed class HomeDashboardResponse
{
    public int GuardianId { get; init; }

    public IReadOnlyList<ChildBalanceItemDto> Children { get; init; } = [];

    public IReadOnlyList<TransactionHistoryItemDto> RecentTransactions { get; init; } = [];
}
