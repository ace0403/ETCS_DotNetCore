namespace ETCS.Shared.Infrastructure.Students;

public sealed class GuardianChildrenBalancesResponse
{
    public int GuardianId { get; init; }

    public IReadOnlyList<ChildBalanceItemDto> Children { get; init; } = [];
}
