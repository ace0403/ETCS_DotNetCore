namespace ETCS.Shared.Infrastructure.Students;

public sealed class ChildBalanceItemDto
{
    public string StudentId { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public string CardId { get; init; } = string.Empty;

    public decimal MinimumTopupAmount { get; init; }

    /// <summary>Student CustomerId used as Id No on the card and for replace-card.</summary>
    public string CustomerId { get; init; } = string.Empty;

    public string Grade { get; init; } = string.Empty;

    public string Section { get; init; } = string.Empty;

    public string SchoolName { get; init; } = string.Empty;

    /// <summary>Absolute or root-relative URL to the school logo, when available.</summary>
    public string? SchoolLogoUrl { get; init; }
}
