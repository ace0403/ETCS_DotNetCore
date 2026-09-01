using Asp.Versioning;
using ETCS.Shared.Infrastructure.Meals.Menu;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers.V2;

[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/Meal")]
[Authorize]
public sealed class MealController : ControllerBase
{
    private const int MenuDurationDays = 30;

    private readonly IMealMenuComposer _menuComposer;
    private readonly IStudentRepository _studentRepository;
    private readonly MealOrderBookingWindow _bookingWindow;

    public MealController(
        IMealMenuComposer menuComposer,
        IStudentRepository studentRepository,
        MealOrderBookingWindow bookingWindow)
    {
        _menuComposer = menuComposer;
        _studentRepository = studentRepository;
        _bookingWindow = bookingWindow;
    }

    /// <summary>
    /// Gets the pre-order menu for a student on a date, grouped by meal session with type filters.
    /// </summary>
    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu(
        [FromQuery] int studentId,
        [FromQuery] DateTime mealDate,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required" });
        }

        if (mealDate == default)
        {
            return BadRequest(new { message = "MealDate is required." });
        }

        if (!_bookingWindow.IsBookable(mealDate))
        {
            return BadRequest(new { message = "No items available for this date. The 3:00 PM cutoff has passed." });
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return BadRequest(new { message = "Unable to resolve school for this student." });
        }

        var menu = await _menuComposer.ComposeMenuAsync(studentId, schoolId.Value, mealDate, cancellationToken);
        return Ok(menu);
    }

    /// <summary>
    /// Gets school calendar days for the pre-order date strip (half day, weekend, holiday badges).
    /// When from/to are omitted, the window starts at the Dubai 15:00 booking cutoff and lasts 30 days.
    /// </summary>
    [HttpGet("school-days")]
    public async Task<IActionResult> GetSchoolDays(
        [FromQuery] int studentId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required" });
        }

        var start = from?.Date ?? _bookingWindow.GetEarliestBookableDate();
        var end = to?.Date ?? start.AddDays(MenuDurationDays - 1);

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return BadRequest(new { message = "Unable to resolve school for this student." });
        }

        var days = await _menuComposer.ComposeSchoolDaysAsync(schoolId.Value, start, end, cancellationToken);
        return Ok(days);
    }
}
