using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Admin.Infrastructure.Payments;

public interface IV1TopupRecoveryRepository
{
    Task<IReadOnlyList<V1TopupRecoveryRow>> ListAsync(V1TopupRecoveryFilter filter, IReadOnlyList<int>? schoolIds, CancellationToken ct);
    Task<V1TopupRecoveryRow?> GetAsync(int id, IReadOnlyList<int>? schoolIds, CancellationToken ct);
}

public sealed class V1TopupRecoveryRepository(IMealDbConnectionFactory connections) : IV1TopupRecoveryRepository
{
    // ibonus is also the catalog used by the existing V1 completion repository.
    // Take a bounded page BEFORE looking up wallet entries. Do not scan PGLogs.
    private const string Projection = """
        SELECT p.*, student.StudentMatches, student.CustomerId, student.StudentName,
               receipt.ReceiptCount, receipt.ReceiptAmount, receipt.ReceiptId
        FROM page p
        OUTER APPLY (
            SELECT COUNT(*) AS StudentMatches, MAX(sl.CustomerID) AS CustomerId,
                   MAX(CONCAT(sl.StudFirstName, ' ', sl.StudLastName)) AS StudentName
            FROM ibonus.dbo.StudentLogin sl
            WHERE sl.UserId = CONVERT(varchar(20), p.StudentId)
        ) student
        OUTER APPLY (
            SELECT COUNT(*) AS ReceiptCount, MAX(a.Amount) AS ReceiptAmount,
                   MAX(a.RcdID) AS ReceiptId
            FROM ibonus.dbo.AccessLog a
            WHERE a.TransactionID = p.TransactionId AND a.CustomerID = student.CustomerId
        ) receipt
        ORDER BY p.Id DESC;
        """;

    private const string Columns = """
        t.Id, ISNULL(t.StudentId, 0) AS StudentId,
        LTRIM(RTRIM(ISNULL(t.Remarks, ''))) AS OrderId,
        LTRIM(RTRIM(ISNULL(t.TransactionId, ''))) AS TransactionId,
        ISNULL(t.Amount, 0) AS Amount, ISNULL(t.StatusId, 0) AS StatusId,
        ISNULL(t.IsTransactionCompleted, 0) AS IsCompleted, t.AccessLogId,
        ISNULL(t.SourceChannel, '') AS SourceChannel, ISNULL(t.PaymentMethod, 0) AS PaymentMethod,
        t.CreatedOn, t.LastReconcileOn, ISNULL(t.ReconcileAttemptCount, 0) AS ReconcileAttemptCount
        """;

    private const string Scope = """
        NOT EXISTS (SELECT 1 FROM [Order] o WHERE o.TransactionId = t.Id)
        AND ISNULL(t.PaymentMethod, 0) IN (0, 4)
        AND (@Unrestricted = 1 OR EXISTS (
            SELECT 1 FROM ibonus.dbo.StudentLogin scoped
            WHERE scoped.UserId = CONVERT(varchar(20), t.StudentId)
              AND scoped.StudSchoolId IN @SchoolIds
        ))
        """;

    public async Task<IReadOnlyList<V1TopupRecoveryRow>> ListAsync(
        V1TopupRecoveryFilter filter, IReadOnlyList<int>? schoolIds, CancellationToken ct)
    {
        if (schoolIds is { Count: 0 }) return [];
        var sql = $"""
            WITH page AS (
                SELECT TOP (51) {Columns}
                FROM [Transaction] t
                WHERE {Scope}
                  AND t.CreatedOn >= @From AND t.CreatedOn < @Until
                  AND (@BeforeId = 0 OR t.Id < @BeforeId)
                  AND (@IncludeCompleted = 1 OR ISNULL(t.IsTransactionCompleted, 0) = 0)
                  AND (@Reference = '' OR t.Remarks = @Reference OR t.TransactionId = @Reference)
                ORDER BY t.Id DESC
            )
            {Projection}
            """;
        return await QueryAsync(sql, new {
            filter.From, Until = filter.To.Date.AddDays(1), filter.BeforeId, filter.IncludeCompleted,
            Reference = filter.Reference?.Trim() ?? "", Unrestricted = schoolIds is null,
            SchoolIds = schoolIds?.ToArray() ?? [0]
        }, ct);
    }

    public async Task<V1TopupRecoveryRow?> GetAsync(int id, IReadOnlyList<int>? schoolIds, CancellationToken ct)
    {
        if (id <= 0 || schoolIds is { Count: 0 }) return null;
        var sql = $"""
            WITH page AS (
                SELECT {Columns}
                FROM [Transaction] t WHERE t.Id = @Id AND {Scope}
            )
            {Projection}
            """;
        var rows = await QueryAsync(sql, new {
            Id = id, Unrestricted = schoolIds is null, SchoolIds = schoolIds?.ToArray() ?? [0]
        }, ct);
        return rows.SingleOrDefault();
    }

    private async Task<IReadOnlyList<V1TopupRecoveryRow>> QueryAsync(string sql, object args, CancellationToken ct)
    {
        using var connection = (DbConnection)connections.CreateConnection();
        await connection.OpenAsync(ct);
        return (await connection.QueryAsync<V1TopupRecoveryRow>(
            new CommandDefinition(sql, args, commandTimeout: 30, cancellationToken: ct))).ToList();
    }
}
