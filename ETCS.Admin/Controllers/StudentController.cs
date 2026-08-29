using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class StudentController : Controller
{
    private readonly IStudentAdminRepository _repository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public StudentController(
        IStudentAdminRepository repository,
        IMealEnumAdminRepository mealEnumRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _mealEnumRepository = mealEnumRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        await SetStudentLookupsAsync(cancellationToken);
        return View(new StudentAdminSaveRequest());
    }

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetDataAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Get(decimal id, CancellationToken cancellationToken)
    {
        await SetStudentLookupsAsync(cancellationToken);
        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new StudentAdminSaveRequest()
            : new StudentAdminSaveRequest();

        if (id > 0)
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }

        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(StudentAdminSaveRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { Success = false, Message = "Required fields are missing." });
        }

        try
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }
        catch (UnauthorizedAccessException)
        {
            return Json(new { Success = false, Message = "You do not have access to this school." });
        }

        var result = await _repository.SaveAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> Delete(decimal id, CancellationToken cancellationToken)
    {
        var existing = await _repository.GetAsync(id, cancellationToken);
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

        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    private async Task SetStudentLookupsAsync(CancellationToken cancellationToken)
    {
        var guardians = await _repository.GuardianLookupsAsync(cancellationToken);
        var schools = await _repository.SchoolLookupsAsync(cancellationToken);
        var grades = await _repository.GradeLookupsAsync(cancellationToken);
        var allergies = await _mealEnumRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        var orderTypes = await _mealEnumRepository.GetStudentOrderTypesAsync(cancellationToken);

        ViewBag.Guardians = new SelectList(guardians, "Id", "Name");
        ViewBag.Grades = grades;
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        ViewBag.Allergies = new SelectList(allergies, "Id", "Name");
        ViewBag.OrderTypes = new SelectList(orderTypes, "Id", "Name");
    }
}
