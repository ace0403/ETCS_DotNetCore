using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Audit;

/// <summary>
/// Payment-gateway returns are anonymous. Attribute those audits to the guardian who placed the order.
/// </summary>
internal static class AuditGuardianActor
{
    public static bool ShouldAttach()
    {
        var ambient = AuditAmbientContext.Current;
        if (ambient is null)
        {
            return true;
        }

        if (string.Equals(ambient.ActorType, AuditActorTypes.System, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ambient.ActorId is not > 0;
    }

    public static async Task<(int Id, string? Name)?> TryResolveAsync(
        IDbConnectionFactory connectionFactory,
        int guardianId,
        int studentId,
        CancellationToken cancellationToken)
    {
        if (!ShouldAttach() || (guardianId <= 0 && studentId <= 0))
        {
            return null;
        }

        try
        {
            using var connection = connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            var row = await dbConnection.QuerySingleOrDefaultAsync<GuardianActorRow>(
                new CommandDefinition(
                    """
                    SELECT TOP (1)
                        CAST(g.GrdID AS int) AS Id,
                        LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS Name
                    FROM GuardianInfo g
                    WHERE (@GuardianId > 0 AND g.GrdID = @GuardianId)
                       OR (@GuardianId <= 0 AND g.GrdID = (
                            SELECT TOP (1) sl.GrdID
                            FROM StudentLogin sl
                            WHERE sl.UserId = @StudentId));
                    """,
                    new { GuardianId = guardianId, StudentId = studentId },
                    cancellationToken: cancellationToken));

            if (row is null || row.Id <= 0)
            {
                return guardianId > 0 ? (guardianId, null) : null;
            }

            var name = string.IsNullOrWhiteSpace(row.Name) ? null : row.Name.Trim();
            return (row.Id, name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return guardianId > 0 ? (guardianId, null) : null;
        }
    }

    private sealed class GuardianActorRow
    {
        public int Id { get; init; }

        public string Name { get; init; } = string.Empty;
    }
}
