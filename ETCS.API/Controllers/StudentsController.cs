using ETCS.API.Infrastructure.Auth;
using ETCS.API.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class StudentsController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;

    public StudentsController(IStudentRepository studentRepository)
    {
        _studentRepository = studentRepository;
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
    /// </summary>
    [HttpGet("balances")]
    public async Task<IActionResult> GetMyChildrenBalances(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var children = await ChildBalanceItemFactory.CreateAsync(students, _studentRepository, cancellationToken);

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

    /// <summary>Creates a student (<c>spInsertStudentInfo</c>). Password is hashed (MD5) before calling the database.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateStudent(
        [FromBody] UpsertStudentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StudCode))
        {
            return BadRequest(new { message = "StudCode is required." });
        }

        try
        {
            await _studentRepository.SaveStudentAsync(request, isInsert: true, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "Student created." });
    }

    /// <summary>Updates a student (<c>spInsertStudentInfo</c>). Omit <c>studPassword</c> to leave the password unchanged (NULL sent to SQL).</summary>
    [HttpPut]
    public async Task<IActionResult> UpdateStudent(
        [FromBody] UpsertStudentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.StudCode))
        {
            return BadRequest(new { message = "StudCode is required." });
        }

        try
        {
            await _studentRepository.SaveStudentAsync(request, isInsert: false, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new { message = "Student updated." });
    }
}
