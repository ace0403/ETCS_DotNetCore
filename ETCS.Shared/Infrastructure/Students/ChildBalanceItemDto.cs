namespace ETCS.Shared.Infrastructure.Students;

public sealed class ChildBalanceItemDto
{
    public string StudentId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public string CardId { get; init; } = string.Empty;

    public decimal MinimumTopupAmount { get; init; }
}
