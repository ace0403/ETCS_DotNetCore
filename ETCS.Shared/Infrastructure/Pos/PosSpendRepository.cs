using System.Data;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosSpendRepository : IPosSpendRepository
{
    private readonly IDbConnectionFactory _ibonusConnectionFactory;
    private readonly IPOSOrderRepository _posOrderRepository;

    public PosSpendRepository(
        IDbConnectionFactory ibonusConnectionFactory,
        IPOSOrderRepository posOrderRepository)
    {
        _ibonusConnectionFactory = ibonusConnectionFactory;
        _posOrderRepository = posOrderRepository;
    }

    public async Task<PosSpendInfoDto?> GetSpendInfoByCustomerIdAsync(
        string customerId,
        int orderTypeId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId,
                sl.UserId AS StudentId,
                CAST(ISNULL(sl.DailyLimit, 0) AS decimal(18,2)) AS DailySpendLimit,
                CAST(ISNULL(sl.WeeklyLimit, 0) AS decimal(18,2)) AS WeeklySpendLimit,
                sl.GrdId AS GuardianId
            FROM StudentLogin sl
            WHERE LTRIM(RTRIM(sl.CustomerId)) = @CustomerId;
            """;

        using var connection = _ibonusConnectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<SpendRow>(new CommandDefinition(
            sql,
            new { CustomerId = customerId.Trim() },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        if (row is null || row.StudentId <= 0)
        {
            return null;
        }

        var snapshot = await _posOrderRepository.GetSpendingSnapshotAsync(
            row.StudentId,
            row.GuardianId,
            orderTypeId,
            DateTime.Today,
            cancellationToken);

        var dailyRemaining = Math.Max(0, row.DailySpendLimit - snapshot.DailySpent);
        var weeklyRemaining = Math.Max(0, row.WeeklySpendLimit - snapshot.WeeklySpent);

        return new PosSpendInfoDto
        {
            CustomerId = row.CustomerId,
            StudentId = row.StudentId,
            DailySpent = snapshot.DailySpent,
            WeeklySpent = snapshot.WeeklySpent,
            DailySpendLimit = row.DailySpendLimit,
            WeeklySpendLimit = row.WeeklySpendLimit,
            DailyRemaining = dailyRemaining,
            WeeklyRemaining = weeklyRemaining,
            IsDailyLimitExceeded = snapshot.DailySpent > row.DailySpendLimit && row.DailySpendLimit > 0,
            IsWeeklyLimitExceeded = snapshot.WeeklySpent > row.WeeklySpendLimit && row.WeeklySpendLimit > 0
        };
    }

    private sealed class SpendRow
    {
        public string CustomerId { get; init; } = string.Empty;
        public int StudentId { get; init; }
        public int GuardianId { get; init; }
        public decimal DailySpendLimit { get; init; }
        public decimal WeeklySpendLimit { get; init; }
    }
}
