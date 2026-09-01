using System.Globalization;
using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class SchoolCalendarController : Controller
{
    private readonly ISchoolCalendarRepository _repository;
    private readonly IStudentAdminRepository _studentAdminRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public SchoolCalendarController(
        ISchoolCalendarRepository repository,
        IStudentAdminRepository studentAdminRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _studentAdminRepository = studentAdminRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> Index(int? schoolId, CancellationToken cancellationToken)
    {
        var schools = _schoolScope.FilterSchools(
            await _studentAdminRepository.SchoolLookupsAsync(cancellationToken),
            s => s.Id);

        var selectedSchoolId = schoolId is > 0 && schools.Any(s => s.Id == schoolId.Value)
            ? schoolId.Value
            : schools.FirstOrDefault()?.Id ?? 0;

        IReadOnlyList<SchoolWeeklyDayDto> weekly = [];
        if (selectedSchoolId > 0)
        {
            weekly = await _repository.GetWeeklyAsync(selectedSchoolId, cancellationToken);
        }

        ViewBag.Schools = schools;
        ViewBag.SelectedSchoolId = selectedSchoolId;
        ViewBag.WeeklyDays = weekly;
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> SaveWeekly(SchoolWeeklyScheduleSaveRequest model, CancellationToken cancellationToken)
    {
        if (model.SchoolId <= 0)
        {
            return Json(new { Success = false, Message = "School is required." });
        }

        try
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        var result = await _repository.SaveWeeklyAsync(model.SchoolId, model.Days ?? [], cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    [HttpPost]
    public async Task<JsonResult> GetExceptionList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetExceptionsPagedAsync(request, cancellationToken);

        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        var schoolNames = _schoolScope.FilterSchools(schools, s => s.Id).ToDictionary(s => s.Id, s => s.Name);

        foreach (var row in response.Data)
        {
            if (schoolNames.TryGetValue(row.SchoolId, out var name))
            {
                row.SchoolName = name;
            }

            row.DayStatusLabel = FormatStatus(row.DayStatus);
        }

        return Json(response);
    }

    public async Task<IActionResult> GetException(int id, CancellationToken cancellationToken)
    {
        var schools = _schoolScope.FilterSchools(
            await _studentAdminRepository.SchoolLookupsAsync(cancellationToken),
            s => s.Id);
        ViewBag.Schools = schools;

        SchoolCalendarExceptionSaveRequest model;
        if (id > 0)
        {
            var existing = await _repository.GetExceptionByIdAsync(id, cancellationToken);
            if (existing is null)
            {
                model = new SchoolCalendarExceptionSaveRequest
                {
                    DayStatus = (byte)SchoolDayStatus.Holiday
                };
            }
            else
            {
                _schoolScope.EnsureInScope(existing.SchoolId);
                model = new SchoolCalendarExceptionSaveRequest
                {
                    Id = existing.Id,
                    SchoolId = existing.SchoolId,
                    ExceptionDate = existing.ExceptionDate,
                    DayStatus = existing.DayStatus,
                    Title = existing.Title,
                    Notes = existing.Notes
                };
            }
        }
        else
        {
            model = new SchoolCalendarExceptionSaveRequest
            {
                DayStatus = (byte)SchoolDayStatus.Holiday,
                ExceptionDate = DateTime.Today
            };
        }

        return PartialView("_ExceptionAddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> SaveException(SchoolCalendarExceptionSaveRequest model, CancellationToken cancellationToken)
    {
        if (model.SchoolId <= 0)
        {
            return Json(new { Success = false, Message = "School is required." });
        }

        try
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        var result = await _repository.SaveExceptionAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> DeleteException(int id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetExceptionByIdAsync(id, cancellationToken);
        if (existing is not null)
        {
            try
            {
                _schoolScope.EnsureInScope(existing.SchoolId);
            }
            catch (UnauthorizedAccessException)
            {
                return Json(new { Success = false, Message = "You do not have access to this school." });
            }
        }

        var result = await _repository.DeleteExceptionAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    private static string FormatStatus(byte status) =>
        status switch
        {
            (byte)SchoolDayStatus.Holiday => "Holiday",
            (byte)SchoolDayStatus.HalfDay => "Half day",
            (byte)SchoolDayStatus.FullDay => "Full day",
            _ => status.ToString(CultureInfo.InvariantCulture)
        };
}
