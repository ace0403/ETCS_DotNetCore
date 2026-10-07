using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Infrastructure.Audit;

public sealed class AuditLogWriter : IAuditLogWriter
{
    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly IDbConnectionFactory _legacyConnectionFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditLogWriter> _logger;

    public AuditLogWriter(
        IMealDbConnectionFactory connectionFactory,
        IDbConnectionFactory legacyConnectionFactory,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditLogWriter> logger)
    {
        _connectionFactory = connectionFactory;
        _legacyConnectionFactory = legacyConnectionFactory;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task WriteAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var ambient = AuditAmbientContext.Current;
        ambient?.MarkEntityRecorded();
        if (_httpContextAccessor.HttpContext is { } httpContext)
        {
            httpContext.Items[AuditAmbientContext.RecordedItemKey] = true;
        }

        try
        {
            var actorType = entry.OverrideActor
                ? entry.ActorType ?? AuditActorTypes.Anonymous
                : ambient?.ActorType ?? AuditActorTypes.System;
            int? actorId = entry.OverrideActor ? entry.ActorId : ambient?.ActorId;
            var actorName = entry.OverrideActor ? entry.ActorName : ambient?.ActorName;

            using var connection = _connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO dbo.AuditLog
                    (
                        OccurredOnUtc, CorrelationId, SourceApp, ActorType, ActorId, ActorName,
                        Action, EntityType, EntityId, Outcome, Message,
                        HttpMethod, Path, IpAddress, UserAgent, OldValues, NewValues
                    )
                    VALUES
                    (
                        SYSUTCDATETIME(), @CorrelationId, @SourceApp, @ActorType, @ActorId, @ActorName,
                        @Action, @EntityType, @EntityId, @Outcome, @Message,
                        @HttpMethod, @Path, @IpAddress, @UserAgent, @OldValues, @NewValues
                    );
                    """,
                    new
                    {
                        CorrelationId = ambient?.CorrelationId ?? Guid.NewGuid(),
                        SourceApp = Truncate(ambient?.SourceApp ?? AuditSourceApps.System, 32),
                        ActorType = Truncate(actorType, 32),
                        ActorId = actorId,
                        ActorName = Truncate(actorName, 256),
                        Action = Truncate(entry.Action, 64),
                        EntityType = Truncate(entry.EntityType, 128),
                        EntityId = Truncate(entry.EntityId, 64),
                        Outcome = Truncate(entry.Outcome, 32),
                        Message = Truncate(entry.Message, 1000),
                        HttpMethod = Truncate(ambient?.HttpMethod, 16),
                        Path = Truncate(ambient?.Path, 512),
                        IpAddress = Truncate(ambient?.IpAddress, 64),
                        UserAgent = Truncate(ambient?.UserAgent, 512),
                        OldValues = AuditJson.Serialize(await AuditReferenceRewriter.RewriteAsync(
                            entry.OldValues, _legacyConnectionFactory, _connectionFactory, cancellationToken)),
                        NewValues = AuditJson.Serialize(await AuditReferenceRewriter.RewriteAsync(
                            entry.NewValues, _legacyConnectionFactory, _connectionFactory, cancellationToken))
                    },
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Audit log write failed for {EntityType} {Action}.",
                entry.EntityType,
                entry.Action);
        }
    }

    private static string? Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        return value[..maxLength];
    }
}
