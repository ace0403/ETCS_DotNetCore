using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Notifications;

public sealed class GuardianNotificationRepository : IGuardianNotificationRepository
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const string CreateSp = "spCreateGuardianNotification";
    private const string GetByGuardianSp = "spGetGuardianNotifications";
    private const string GetByIdSp = "spGetGuardianNotificationById";
    private const string UnreadCountSp = "spGetGuardianUnreadNotificationCount";
    private const string MarkReadSp = "spMarkGuardianNotificationRead";
    private const string MarkAllReadSp = "spMarkAllGuardianNotificationsRead";
    private const string AdminLogSp = "spAdminGetNotificationLog";

    private const string GuardianIdsBySchoolSql = """
        SELECT DISTINCT CAST(sl.GrdId AS INT) AS GuardianId
        FROM StudentLogin sl
        WHERE sl.StudSchoolId = @SchoolId
          AND sl.GrdId IS NOT NULL
          AND CAST(sl.GrdId AS INT) > 0;
        """;

    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly IDbConnectionFactory _connectionFactory;

    public GuardianNotificationRepository(
        IMealDbConnectionFactory mealDbConnectionFactory,
        IDbConnectionFactory connectionFactory)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _connectionFactory = connectionFactory;
    }

    public async Task<long> CreateAsync(CreateGuardianNotificationRequest request, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<long>(
            new CommandDefinition(
                CreateSp,
                new
                {
                    request.GuardianId,
                    request.StudentId,
                    request.SchoolId,
                    request.Type,
                    request.Title,
                    request.Message,
                    request.ReferenceType,
                    request.ReferenceId,
                    request.CreatedBy
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<int> CreateForSchoolAsync(
        CreateSchoolBroadcastNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var guardianIds = await GetGuardianIdsBySchoolAsync(request.SchoolId, cancellationToken);
        var inserted = 0;

        foreach (var guardianId in guardianIds)
        {
            await CreateAsync(
                new CreateGuardianNotificationRequest
                {
                    GuardianId = guardianId,
                    SchoolId = request.SchoolId,
                    Type = request.Type,
                    Title = request.Title,
                    Message = request.Message,
                    CreatedBy = request.CreatedBy
                },
                cancellationToken);
            inserted++;
        }

        return inserted;
    }

    public async Task<GuardianNotificationPageDto> GetByGuardianPagedAsync(
        int guardianId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        if (page <= 0)
        {
            page = 1;
        }

        if (pageSize <= 0)
        {
            pageSize = 50;
        }

        if (pageSize > 100)
        {
            pageSize = 100;
        }

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await using var multi = await dbConnection.QueryMultipleAsync(
            new CommandDefinition(
                GetByGuardianSp,
                new
                {
                    GuardianId = guardianId,
                    Page = page,
                    PageSize = pageSize,
                    UnreadOnly = unreadOnly
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        var totalCount = await multi.ReadFirstAsync<int>();
        var items = (await multi.ReadAsync<GuardianNotificationDto>()).ToList();

        return new GuardianNotificationPageDto
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<IReadOnlyList<GuardianNotificationDto>> GetByGuardianAsync(
        int guardianId,
        int top,
        bool unreadOnly,
        CancellationToken cancellationToken)
    {
        var page = await GetByGuardianPagedAsync(guardianId, 1, top, unreadOnly, cancellationToken);
        return page.Items;
    }

    public async Task<GuardianNotificationDto?> GetByIdForGuardianAsync(
        int guardianId,
        long notificationId,
        CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QuerySingleOrDefaultAsync<GuardianNotificationDto>(
            new CommandDefinition(
                GetByIdSp,
                new { GuardianId = guardianId, NotificationId = notificationId },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<int> GetUnreadCountAsync(int guardianId, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                UnreadCountSp,
                new { GuardianId = guardianId },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<int> MarkReadAsync(int guardianId, long notificationId, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                MarkReadSp,
                new { GuardianId = guardianId, NotificationId = notificationId },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<int> MarkAllReadAsync(int guardianId, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                MarkAllReadSp,
                new { GuardianId = guardianId },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<GuardianNotificationDto>> GetAdminLogAsync(
        int top,
        int? guardianId,
        int? schoolId,
        string? type,
        CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<GuardianNotificationDto>(
            new CommandDefinition(
                AdminLogSp,
                new
                {
                    Top = top,
                    GuardianId = guardianId,
                    SchoolId = schoolId,
                    Type = type
                },
                commandType: CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private async Task<IReadOnlyList<int>> GetGuardianIdsBySchoolAsync(int schoolId, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<int>(
            new CommandDefinition(
                GuardianIdsBySchoolSql,
                new { SchoolId = schoolId },
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }
}
