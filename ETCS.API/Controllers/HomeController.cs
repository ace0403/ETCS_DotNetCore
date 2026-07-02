using ETCS.API.Infrastructure.Auth;
using ETCS.API.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Home;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class HomeController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;

    public HomeController(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
    }

    /// <summary>
    /// Guardian dashboard: child balances and recent transactions in one call.
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

        var studentsTask = _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var historyTask = _transactionRepository.GetTransactionHistoryAsync(
            studentId: null,
            guardianId: guardianId,
            type: "all",
            page: 1,
            pageSize: recentCount,
            cancellationToken);

        await Task.WhenAll(studentsTask, historyTask);

        var students = await studentsTask;
        var history = await historyTask;

        var children = await ChildBalanceItemFactory.CreateAsync(students, _studentRepository, cancellationToken);

        return Ok(new HomeDashboardResponse
        {
            GuardianId = guardianId,
            Children = children,
            RecentTransactions = history.Items
        });
    }
}
