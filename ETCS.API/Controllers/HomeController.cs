using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Home;
using ETCS.Shared.Infrastructure.Transaction;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class HomeController : ControllerBase
{
    private readonly ITransactionRepository _transactionRepository;

    public HomeController(ITransactionRepository transactionRepository)
    {
        _transactionRepository = transactionRepository;
    }

    /// <summary>
    /// Guardian dashboard: recent transactions.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard(
        [FromQuery] int recentCount = 5,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (recentCount <= 0 || recentCount > 50)
        {
            return BadRequest(new { message = "recentCount must be between 1 and 50." });
        }

        var history = await _transactionRepository.GetTransactionHistoryAsync(
            studentId: null,
            guardianId: guardianId,
            type: "all",
            fromDate: null,
            toDate: null,
            page: 1,
            pageSize: recentCount,
            cancellationToken);

        return Ok(new HomeDashboardResponse
        {
            GuardianId = guardianId,
            RecentTransactions = history.Items
        });
    }
}
