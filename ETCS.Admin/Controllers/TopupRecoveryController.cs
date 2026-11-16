using ETCS.Admin.Infrastructure.Auth;
using ETCS.Admin.Infrastructure.Payments;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TopupRecoveryController(
    IV1TopupRecoveryRepository repository,
    V1TopupRecoveryService recovery,
    IAdminSchoolScopeService schools,
    ILogger<TopupRecoveryController> logger) : Controller
{
    private bool CanView => User.HasPermission("Report.AdminTransaction.View");
    private bool CanRetry => CanView && User.HasPermission("Report.AdminTransaction.Edit");
    private IReadOnlyList<int>? SchoolIds => schools.IsUnrestricted ? null : schools.GetAllowedSchoolIds();

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] V1TopupRecoveryFilter filter, CancellationToken ct)
    {
        if (!CanView) return Forbid();
        if (!ModelState.IsValid || filter.From.Year < 2000 || filter.To.Year > 2100 ||
            filter.To.Date < filter.From.Date || (filter.To.Date - filter.From.Date).Days > 31 ||
            filter.BeforeId < 0 || filter.Reference?.Length > 100)
            return BadRequest("Choose a valid request date range of up to 31 days and a reference of up to 100 characters.");
        filter.From = filter.From.Date;
        filter.To = filter.To.Date;
        var rows = await repository.ListAsync(filter, SchoolIds, ct);
        ViewBag.Filter = filter;
        ViewBag.CanRetry = CanRetry;
        ViewBag.RecoveryEnabled = recovery.Enabled;
        ViewBag.HasMore = rows.Count > 50;
        return View(rows.Take(50).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry([FromForm] int id, CancellationToken ct)
    {
        if (!CanRetry) return Forbid();
        if (!ModelState.IsValid || id <= 0)
            return BadRequest(new { Success = false, StopBatch = true, Message = "Choose a valid payment." });
        try
        {
            var result = await recovery.RetryAsync(id, SchoolIds, ct);
            return Json(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin V1 recovery request did not complete. TransactionPkId={TransactionPkId}", id);
            return StatusCode(503, new { Success = false, StopBatch = true,
                Message = "The payment check could not finish. Refresh the list and check before retrying." });
        }
    }
}
