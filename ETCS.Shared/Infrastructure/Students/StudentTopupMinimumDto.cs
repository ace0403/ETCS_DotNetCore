namespace ETCS.Shared.Infrastructure.Students;

public sealed class StudentTopupMinimumDto
{
    public string StudentId { get; init; } = string.Empty;

    public decimal MinimumTopupAmount { get; init; }
}
