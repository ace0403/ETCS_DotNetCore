using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Master.Guardians;
using ETCS.Web.Infrastructure.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

[Authorize]
public class MyKidsController : Controller
{
    private readonly IGuardianAdminRepository _repository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;

    public MyKidsController(
        IGuardianAdminRepository repository,
        IMealEnumAdminRepository mealEnumRepository)
    {
        _repository = repository;
        _mealEnumRepository = mealEnumRepository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await _repository.GetChildrenViewAsync(guardianId, cancellationToken)
            ?? new GuardianChildrenViewModel { GuardianId = guardianId };

        return View(model);
    }

    public async Task<IActionResult> GetList(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await _repository.GetChildrenViewAsync(guardianId, cancellationToken)
            ?? new GuardianChildrenViewModel { GuardianId = guardianId };

        return PartialView("_KidsList", model);
    }

    public async Task<IActionResult> GetAddView(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await _repository.GetAddStudentViewAsync(guardianId, cancellationToken);
        if (model is null)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">Unable to load the add form.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
        }

        ViewBag.Grades = model.Grades;
        ViewBag.Schools = model.Schools;
        var allergies = await _mealEnumRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        ViewBag.Allergies = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(allergies, "Id", "Name");

        return PartialView("_AddStudent", new GuardianAddStudentRequest
        {
            GuardianId = guardianId,
            Gender = "Male"
        });
    }

    [HttpPost]
    public async Task<JsonResult> AddStudent(GuardianAddStudentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        request.GuardianId = guardianId;
        var result = await _repository.AddStudentAsync(request, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<IActionResult> GetEditView(decimal userId, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await _repository.GetEditStudentViewAsync(guardianId, userId, cancellationToken);
        if (model is null)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">Student was not found.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
        }

        ViewBag.Grades = model.Grades;
        ViewBag.Schools = model.Schools;
        var allergies = await _mealEnumRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        ViewBag.Allergies = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(allergies, "Id", "Name");

        return PartialView("_EditStudent", model.Student);
    }

    [HttpPost]
    public async Task<JsonResult> EditStudent(GuardianEditStudentRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        request.GuardianId = guardianId;
        var result = await _repository.EditStudentAsync(request, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }
}
