using System.Globalization;
using ETCS.API.Infrastructure.Auth;
using ETCS.API.Infrastructure.Students;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Media;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class StudentsController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly IReplaceCardRequestRepository _replaceCardRequestRepository;
    private readonly IGuardianChildEnrollmentService _childEnrollmentService;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;
    private readonly MealImageUrlBuilder _imageUrlBuilder;

    public StudentsController(
        IStudentRepository studentRepository,
        IReplaceCardRequestRepository replaceCardRequestRepository,
        IGuardianChildEnrollmentService childEnrollmentService,
        IGuardianEmailNotificationService emailNotificationService,
        IStudentOrderTypeAccessService orderTypeAccess,
        MealImageUrlBuilder imageUrlBuilder)
    {
        _studentRepository = studentRepository;
        _replaceCardRequestRepository = replaceCardRequestRepository;
        _childEnrollmentService = childEnrollmentService;
        _emailNotificationService = emailNotificationService;
        _orderTypeAccess = orderTypeAccess;
        _imageUrlBuilder = imageUrlBuilder;
    }

    /// <summary>
    /// Lists students for a guardian (same student records as elsewhere in the API).
    /// </summary>
    [HttpGet("list")]
    public async Task<IActionResult> ListByGuardian(
        [FromQuery] string? customerId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var students = await _studentRepository.GetStudentsByGuardianAsync(
            guardianId,
            customerId?.Trim(),
            cancellationToken);

        return Ok(students);
    }

    /// <summary>
    /// Balance for each child linked to the logged-in guardian (JWT).
    /// When <paramref name="orderTypeId"/> is provided, only children allowed for that order type are returned.
    /// </summary>
    [HttpGet("balances")]
    public async Task<IActionResult> GetMyChildrenBalances(
        [FromQuery] int? orderTypeId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (orderTypeId is > 0 && !StudentOrderTypeOptionIds.Selectable.Contains(orderTypeId.Value))
        {
            return BadRequest(new { message = "Invalid orderTypeId." });
        }

        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        if (orderTypeId is > 0)
        {
            var allowedIds = (await _orderTypeAccess.FilterAllowedAsync(
                students.Select(s => Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture)),
                orderTypeId.Value,
                cancellationToken)).ToHashSet();

            students = students
                .Where(s => allowedIds.Contains(Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture)))
                .ToList();
        }
        else
        {
            students = students
                .Where(s => s.IsNoService != 1)
                .ToList();
        }

        var children = await ChildBalanceItemFactory.CreateAsync(
            students,
            _studentRepository,
            _imageUrlBuilder,
            cancellationToken);

        return Ok(new GuardianChildrenBalancesResponse
        {
            GuardianId = guardianId,
            Children = children
        });
    }

    /// <summary>
    /// Lists only studentId, guardianId and name for a guardian.
    /// </summary>
    [HttpGet("basic-list")]
    public async Task<IActionResult> GetBasicList(
        [FromQuery] int guardianId,
        CancellationToken cancellationToken)
    {
        if (guardianId <= 0)
        {
            return BadRequest(new { message = "GuardianId is required." });
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        return Ok(students);
    }

    /// <summary>
    /// Gets guardian basic details and customerId by studentId.
    /// </summary>
    [HttpGet("basic-detail")]
    public async Task<IActionResult> GetBasicDetailByStudentId(
        [FromQuery] string studentId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        var detail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(studentId, cancellationToken);
        if (detail is null)
        {
            return NotFound(new { message = "No guardian details found for this studentId." });
        }

        return Ok(detail);
    }

    /// <summary>
    /// Student summary for dashboard (same student entity).
    /// </summary>
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] string? studentId,
        [FromQuery] int guardianId,
        CancellationToken cancellationToken)
    {
        var summary = await _studentRepository.GetStudentSummaryAsync(studentId, guardianId, cancellationToken);
        return Ok(summary);
    }

    /// <summary>Grade / year group list for dropdowns (<c>spGetAllGrades</c>).</summary>
    [HttpGet("grades")]
    public async Task<IActionResult> GetGrades(CancellationToken cancellationToken)
    {
        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        return Ok(grades);
    }

    /// <summary>School list for dropdowns (<c>spSelectSchoolInfoByCountryID</c>).</summary>
    [HttpGet("schools")]
    public async Task<IActionResult> GetSchools(
        [FromQuery] int countryId,
        [FromQuery] string? schoolId,
        CancellationToken cancellationToken)
    {
        var schools = await _studentRepository.GetSchoolsByCountryAsync(countryId, schoolId, cancellationToken);
        return Ok(schools);
    }

    [HttpGet("add-child-form")]
    public async Task<IActionResult> GetAddChildForm(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out _))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var form = await _childEnrollmentService.GetAddChildFormAsync(cancellationToken);
        return Ok(form);
    }

    [HttpGet("edit-child")]
    public async Task<IActionResult> GetEditChildForm(
        [FromQuery] string studentId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (!decimal.TryParse(studentId, out var userId) || userId <= 0)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        var form = await _childEnrollmentService.GetEditChildFormAsync(guardianId, userId, cancellationToken);
        if (form is null)
        {
            return NotFound(new { message = "Student was not found for this parent." });
        }

        return Ok(form);
    }

    [HttpPost]
    public async Task<IActionResult> CreateStudent(
        [FromBody] GuardianChildUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _childEnrollmentService.CreateAsync(guardianId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    [HttpPut]
    public async Task<IActionResult> UpdateStudent(
        [FromBody] GuardianChildUpsertRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _childEnrollmentService.UpdateAsync(guardianId, request, cancellationToken);
        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new { message = result.Message });
    }

    /// <summary>
    /// Submits a replace-card request for a child card owned by the logged-in guardian.
    /// </summary>
    [HttpPost("replace-card")]
    public async Task<IActionResult> SubmitReplaceCard(
        [FromBody] ReplaceCardSubmitRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return BadRequest(new { message = "CustomerId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.CardNumber))
        {
            return BadRequest(new { message = "CardNumber is required." });
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return BadRequest(new { message = "Reason is required." });
        }

        var result = await _replaceCardRequestRepository.CreateAsync(
            guardianId,
            request.CustomerId,
            request.CardNumber,
            request.Reason,
            cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { message = result.Message });
        }

        await _emailNotificationService.QueueReplaceCardRequestAsync(
            guardianId,
            request.CustomerId,
            request.CardNumber,
            request.Reason,
            result.RefCode,
            cancellationToken);

        return Ok(new { message = result.Message, refCode = result.RefCode });
    }

    /// <summary>
    /// Lists replace-card requests for children linked to the logged-in guardian.
    /// </summary>
    [HttpGet("replace-card")]
    public async Task<IActionResult> ListReplaceCardRequests(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var requests = await _replaceCardRequestRepository.GetByGuardianAsync(guardianId, cancellationToken);
        return Ok(requests);
    }
}
