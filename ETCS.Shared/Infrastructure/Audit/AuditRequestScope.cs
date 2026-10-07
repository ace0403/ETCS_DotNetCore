namespace ETCS.Shared.Infrastructure.Audit;

public sealed class AuditRequestScope
{
    public Guid CorrelationId { get; init; } = Guid.NewGuid();

    public string SourceApp { get; init; } = AuditSourceApps.System;

    public string ActorType { get; set; } = AuditActorTypes.Anonymous;

    public int? ActorId { get; set; }

    public string? ActorName { get; set; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? HttpMethod { get; init; }

    public string? Path { get; init; }

    public bool EntityAuditRecorded { get; private set; }

    public void MarkEntityRecorded() => EntityAuditRecorded = true;

    public static AuditRequestScope ForSystem() => new()
    {
        CorrelationId = Guid.NewGuid(),
        SourceApp = AuditSourceApps.System,
        ActorType = AuditActorTypes.System
    };
}
