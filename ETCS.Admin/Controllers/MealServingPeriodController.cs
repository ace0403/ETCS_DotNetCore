using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealServingPeriods;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class MealServingPeriodController : Controller
{
    private readonly IMealServingPeriodAdminRepository _repository;
    private readonly IStudentAdminRepository _studentAdminRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public MealServingPeriodController(
        IMealServingPeriodAdminRepository repository,
        IStudentAdminRepository studentAdminRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _studentAdminRepository = studentAdminRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetDataAsync(request, cancellationToken);
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        var schoolNames = _schoolScope.FilterSchools(schools, s => s.Id).ToDictionary(s => s.Id, s => s.Name);

        foreach (var row in response.Data)
        {
            if (schoolNames.TryGetValue(row.SchoolId, out var name))
            {
                row.SchoolName = name;
            }
        }

        return Json(response);
    }

    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var schools = await _studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new MealServingPeriodSaveRequest()
            : new MealServingPeriodSaveRequest();

        if (id > 0)
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }

        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(MealServingPeriodSaveRequest model, CancellationToken cancellationToken)
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

    public async Task<JsonResult> Delete(int id, CancellationToken cancellationToken)
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
}
