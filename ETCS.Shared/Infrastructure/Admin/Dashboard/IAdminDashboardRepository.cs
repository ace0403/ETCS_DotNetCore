namespace ETCS.Shared.Infrastructure.Admin.Dashboard;

public interface IAdminDashboardRepository
{
    Task<AdminDashboardOverviewDto> GetOverviewAsync(
        AdminDashboardFilter filter,
        CancellationToken cancellationToken = default);
}
