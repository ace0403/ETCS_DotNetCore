namespace ETCS.Shared.Infrastructure.Audit;

public sealed class AuditLogEntry
{
    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string? EntityId { get; init; }

    public string Outcome { get; init; } = AuditOutcomes.Success;

    public string? Message { get; init; }

    public object? OldValues { get; init; }

    public object? NewValues { get; init; }

    public bool OverrideActor { get; init; }

    public string? ActorType { get; init; }

    public int? ActorId { get; init; }

    public string? ActorName { get; init; }
}
