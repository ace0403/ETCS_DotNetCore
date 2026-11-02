using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Audit;

public sealed class AuditLogSearchRequest : DataTableRequest
{
    public DateTime? FromDate { get; set; }

    public DateTime? ToDate { get; set; }

    public string? SourceApp { get; set; }

    public string? Actor { get; set; }

    public string? Action { get; set; }

    public string? EntityType { get; set; }

    public string? EntityId { get; set; }

    public string? Outcome { get; set; }

    public string? CorrelationId { get; set; }
}

public sealed class AuditLogListItemDto
{
    public long AuditLogId { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public string SourceApp { get; init; } = string.Empty;

    public string ActorType { get; init; } = string.Empty;

    public int? ActorId { get; init; }

    public string? ActorName { get; init; }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string? EntityId { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? Message { get; init; }

    public Guid CorrelationId { get; init; }
}

public sealed class AuditLogDetailDto
{
    public long AuditLogId { get; init; }

    public DateTime OccurredOnUtc { get; init; }

    public string SourceApp { get; init; } = string.Empty;

    public string ActorType { get; init; } = string.Empty;

    public int? ActorId { get; init; }

    public string? ActorName { get; init; }

    public string Action { get; init; } = string.Empty;

    public string EntityType { get; init; } = string.Empty;

    public string? EntityId { get; init; }

    public string Outcome { get; init; } = string.Empty;

    public string? Message { get; init; }

    public Guid CorrelationId { get; init; }

    public string? HttpMethod { get; init; }

    public string? Path { get; init; }

    public string? IpAddress { get; init; }

    public string? UserAgent { get; init; }

    public string? OldValues { get; init; }

    public string? NewValues { get; init; }
}
