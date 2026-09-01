using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Dashboard;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class DashboardController : Controller
{
    private readonly IAdminDashboardRepository _dashboardRepository;
    private readonly ICanteenTransactionReportRepository _canteenTransactionReportRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public DashboardController(
        IAdminDashboardRepository dashboardRepository,
        ICanteenTransactionReportRepository canteenTransactionReportRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _dashboardRepository = dashboardRepository;
        _canteenTransactionReportRepository = canteenTransactionReportRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var schools = await _canteenTransactionReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = await _schoolScope.FilterSchoolCodesAsync(schools, cancellationToken);
        return View();
    }

    [HttpGet]
    public async Task<JsonResult> GetOverviewJson(
        DateTime? startDate,
        DateTime? endDate,
        string? schoolCode,
        CancellationToken cancellationToken)
    {
        if (startDate is null || endDate is null)
        {
            return Json(new { Success = false, Message = "Start date and end date are required." });
        }

        if (startDate.Value.Date > endDate.Value.Date)
        {
            return Json(new { Success = false, Message = "Start date should be less than End date." });
        }

        var scope = await ResolveCodeSchoolScopeAsync(schoolCode, cancellationToken);
        if (!scope.Success)
        {
            return Json(new { Success = false, Message = scope.Message });
        }

        var overview = await _dashboardRepository.GetOverviewAsync(
            new AdminDashboardFilter
            {
                StartDate = startDate.Value.Date,
                EndDate = endDate.Value.Date,
                SchoolCode = scope.SchoolCode,
                SchoolCodesCsv = scope.SchoolCodesCsv
            },
            cancellationToken);

        return Json(new
        {
            Success = true,
            overview.Summary,
            overview.DailySeries,
            overview.TypeBreakdown,
            overview.TopTerminals,
            overview.RecentTransactions
        });
    }

    private async Task<(bool Success, string? Message, string SchoolCode, string SchoolCodesCsv)> ResolveCodeSchoolScopeAsync(
        string? schoolCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var codes = await _schoolScope.ResolveSchoolCodesForQueryAsync(schoolCode, cancellationToken);
            return (
                true,
                null,
                codes.Count == 1 ? codes[0] : string.Empty,
                string.Join(",", codes));
        }
        catch (UnauthorizedAccessException)
        {
            return (false, "You do not have access to this school.", string.Empty, string.Empty);
        }
    }
}
