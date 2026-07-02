using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Staff;
using ETCS.Shared.Infrastructure.Admin.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class StaffController : Controller
{
    private readonly IStaffAdminRepository _repository;
    private readonly IRoleAdminRepository _roleRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public StaffController(
        IStaffAdminRepository repository,
        IRoleAdminRepository roleRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _repository = repository;
        _roleRepository = roleRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var schools = await _repository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        return View(new StaffSaveRequest { IsNew = true });
    }

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        _schoolScope.ApplyListScope(request);
        var response = await _repository.GetDataAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        ViewBag.Roles = await GetRoleLookupsAsync(cancellationToken);
        ViewBag.Countries = await _repository.CountryLookupsAsync(cancellationToken);

        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new StaffSaveRequest()
            : new StaffSaveRequest { IsNew = true };
        model.IsNew = id <= 0;

        if (model.CountryId > 0)
        {
            var schools = await _repository.SchoolLookupsByCountryAsync(model.CountryId, cancellationToken);
            ViewBag.Schools = _schoolScope.FilterSchools(schools, s => s.Id);
        }
        else
        {
            ViewBag.Schools = Array.Empty<StaffSchoolLookupDto>();
        }

        if (id > 0)
        {
            _schoolScope.EnsureInScope(model.SchoolId);
        }

        return PartialView("_AddUpdate", model);
    }

    [HttpGet]
    public async Task<JsonResult> SchoolsByCountry(int countryId, CancellationToken cancellationToken)
    {
        var schools = await _repository.SchoolLookupsByCountryAsync(countryId, cancellationToken);
        return Json(_schoolScope.FilterSchools(schools, s => s.Id));
    }

    [HttpPost]
    public async Task<JsonResult> Save(StaffSaveRequest model, CancellationToken cancellationToken)
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

    private async Task<IReadOnlyList<StaffRoleLookupDto>> GetRoleLookupsAsync(CancellationToken cancellationToken)
    {
        try
        {
            var mealRoles = await _roleRepository.RoleLookupsAsync(cancellationToken);
            if (mealRoles.Count > 0)
            {
                return mealRoles
                    .Select(r => new StaffRoleLookupDto { Id = r.Id, Name = r.Name })
                    .ToList();
            }
        }
        catch
        {
            // MealDB AdminRole not deployed yet — fall back to ibonus.RoleInfo.
        }

        return await _repository.RoleLookupsAsync(cancellationToken);
    }
}
