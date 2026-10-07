using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Audit;

public static class AuditLogWriterExtensions
{
    public static Task WriteOutcomeAsync(
        this IAuditLogWriter writer,
        string action,
        string entityType,
        string? entityId,
        bool success,
        string? message,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default,
        string? actorType = null,
        int? actorId = null,
        string? actorName = null,
        bool overrideActor = false)
    {
        ArgumentNullException.ThrowIfNull(writer);
        return writer.WriteAsync(
            new AuditLogEntry
            {
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Outcome = success ? AuditOutcomes.Success : AuditOutcomes.Failure,
                Message = message,
                OldValues = oldValues,
                NewValues = newValues,
                OverrideActor = overrideActor,
                ActorType = actorType,
                ActorId = actorId,
                ActorName = actorName
            },
            cancellationToken);
    }

    public static async Task<AdminOperationResult> RecordAdminAsync(
        this IAuditLogWriter writer,
        string entityType,
        string action,
        string? entityId,
        AdminOperationResult result,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default)
    {
        await writer.WriteOutcomeAsync(
            action,
            entityType,
            entityId,
            result.Success,
            result.Message,
            oldValues,
            newValues,
            cancellationToken);
        return result;
    }
}
