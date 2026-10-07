using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Audit;

public interface IAuditLogQueryRepository
{
    Task<DataTableResponse<AuditLogListItemDto>> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken = default);

    Task<AuditLogDetailDto?> GetAsync(long auditLogId, CancellationToken cancellationToken = default);
}

public sealed class AuditLogQueryRepository : IAuditLogQueryRepository
{
    private const string SelectSql = """
        SELECT AuditLogId,
               OccurredOnUtc,
               SourceApp,
               ActorType,
               ActorId,
               ActorName,
               Action,
               EntityType,
               EntityId,
               Outcome,
               Message,
               CorrelationId
        """;

    private const string FromSql = "FROM dbo.AuditLog";

    private const string BaseFilterSql = """
        (@FromUtc IS NULL OR OccurredOnUtc >= @FromUtc)
        AND (@ToUtc IS NULL OR OccurredOnUtc < @ToUtc)
        AND (@SourceApp IS NULL OR SourceApp = @SourceApp)
        AND (@Actor IS NULL OR ActorName LIKE '%' + @Actor + '%' OR CONVERT(NVARCHAR(20), ActorId) = @Actor)
        AND (@Action IS NULL OR Action = @Action)
        AND (@EntityType IS NULL OR EntityType = @EntityType)
        AND (@EntityId IS NULL OR EntityId = @EntityId)
        AND (@Outcome IS NULL OR Outcome = @Outcome)
        AND (@CorrelationId IS NULL OR CorrelationId = @CorrelationId)
        """;

    private const string SearchFilterSql = """
        ActorName LIKE '%' + @Search + '%'
        OR Action LIKE '%' + @Search + '%'
        OR EntityType LIKE '%' + @Search + '%'
        OR EntityId LIKE '%' + @Search + '%'
        OR Message LIKE '%' + @Search + '%'
        OR SourceApp LIKE '%' + @Search + '%'
        OR CONVERT(NVARCHAR(36), CorrelationId) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["OccurredOnUtc"] = "OccurredOnUtc",
        ["SourceApp"] = "SourceApp",
        ["ActorName"] = "ActorName",
        ["Action"] = "Action",
        ["EntityType"] = "EntityType",
        ["EntityId"] = "EntityId",
        ["Outcome"] = "Outcome",
        ["CorrelationId"] = "CorrelationId"
    };

    private readonly IMealDbConnectionFactory _connectionFactory;

    public AuditLogQueryRepository(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<DataTableResponse<AuditLogListItemDto>> SearchAsync(
        AuditLogSearchRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await DataTablePagingHelper.QueryPagedAsync<AuditLogListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            BaseFilterSql,
            SearchFilterSql,
            SortColumns,
            "OccurredOnUtc",
            request,
            BuildParameters(request),
            cancellationToken,
            "DESC");
    }

    public async Task<AuditLogDetailDto?> GetAsync(long auditLogId, CancellationToken cancellationToken = default)
    {
        if (auditLogId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await dbConnection.QuerySingleOrDefaultAsync<AuditLogDetailDto>(
            new CommandDefinition(
                """
                SELECT AuditLogId,
                       OccurredOnUtc,
                       SourceApp,
                       ActorType,
                       ActorId,
                       ActorName,
                       Action,
                       EntityType,
                       EntityId,
                       Outcome,
                       Message,
                       CorrelationId,
                       HttpMethod,
                       Path,
                       IpAddress,
                       UserAgent,
                       OldValues,
                       NewValues
                FROM dbo.AuditLog
                WHERE AuditLogId = @AuditLogId;
                """,
                new { AuditLogId = auditLogId },
                cancellationToken: cancellationToken));
    }

    private static object BuildParameters(AuditLogSearchRequest request)
    {
        Guid? correlationId = Guid.TryParse(request.CorrelationId, out var parsed) ? parsed : null;
        if (!string.IsNullOrWhiteSpace(request.CorrelationId) && correlationId is null)
        {
            correlationId = Guid.Empty;
        }

        return new
        {
            FromUtc = request.FromDate?.Date,
            ToUtc = request.ToDate?.Date.AddDays(1),
            SourceApp = NullIfBlank(request.SourceApp),
            Actor = NullIfBlank(request.Actor),
            Action = NullIfBlank(request.Action),
            EntityType = NullIfBlank(request.EntityType),
            EntityId = NullIfBlank(request.EntityId),
            Outcome = NullIfBlank(request.Outcome),
            CorrelationId = correlationId
        };
    }

    private static string? NullIfBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
