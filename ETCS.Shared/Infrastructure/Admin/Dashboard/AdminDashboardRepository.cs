using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Dashboard;

public sealed class AdminDashboardRepository : IAdminDashboardRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public AdminDashboardRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<AdminDashboardOverviewDto> GetOverviewAsync(
        AdminDashboardFilter filter,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        using var multi = await dbConnection.QueryMultipleAsync(
            new CommandDefinition(
                "spAdminDashboardOverview_New",
                new
                {
                    StartDate = filter.StartDate.Date,
                    EndDate = filter.EndDate.Date,
                    SchoolCode = filter.SchoolCode?.Trim() ?? string.Empty,
                    SchoolCodesCsv = filter.SchoolCodesCsv?.Trim() ?? string.Empty
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var summary = await multi.ReadSingleAsync<AdminDashboardSummaryDto>();
        var dailySeries = (await multi.ReadAsync<AdminDashboardDailyPointDto>()).ToList();
        var typeBreakdown = (await multi.ReadAsync<AdminDashboardTypeBreakdownDto>()).ToList();
        var topTerminals = (await multi.ReadAsync<AdminDashboardTerminalDto>()).ToList();
        var recentTransactions = (await multi.ReadAsync<AdminDashboardRecentTransactionDto>()).ToList();

        return new AdminDashboardOverviewDto
        {
            Summary = summary,
            DailySeries = dailySeries,
            TypeBreakdown = typeBreakdown,
            TopTerminals = topTerminals,
            RecentTransactions = recentTransactions
        };
    }
}
