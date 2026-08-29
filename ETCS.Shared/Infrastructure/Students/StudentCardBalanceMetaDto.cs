namespace ETCS.Shared.Infrastructure.Students;

/// <summary>
/// Per-student metadata needed to enrich guardian child balances.
/// </summary>
public sealed record StudentCardBalanceMetaDto(
    string? CustomerId,
    decimal? MinimumTopupAmount,
    string? SchoolLogoFileName);
