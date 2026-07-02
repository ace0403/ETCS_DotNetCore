using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Guardians;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class GuardianController : Controller
{
    private readonly IGuardianAdminRepository _repository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;

    public GuardianController(
        IGuardianAdminRepository repository,
        IMealEnumAdminRepository mealEnumRepository)
    {
        _repository = repository;
        _mealEnumRepository = mealEnumRepository;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        var response = await _repository.GetDataAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Get(int id, CancellationToken cancellationToken)
    {
        var model = id > 0
            ? await _repository.GetAsync(id, cancellationToken) ?? new GuardianSaveRequest()
            : new GuardianSaveRequest();
        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(GuardianSaveRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { Success = false, Message = "Required fields are missing." });
        }

        if (User.TryGetLoginAccountId(out var accountId))
        {
            model.CreatedBy ??= accountId;
            model.UpdatedBy = accountId;
        }

        var result = await _repository.SaveAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<IActionResult> GetChildrenView(int id, CancellationToken cancellationToken)
    {
        var model = await _repository.GetChildrenViewAsync(id, cancellationToken);
        if (model is null)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">Parent was not found.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
        }

        return PartialView("_ChildrenList", model);
    }

    public async Task<IActionResult> GetTransferView(int id, CancellationToken cancellationToken)
    {
        var model = await _repository.GetTransferViewAsync(id, cancellationToken);
        if (model is null || model.Children.Count < 2)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">This parent needs at least two children to transfer balance.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
        }

        return PartialView("_TransferBalance", model);
    }

    [HttpPost]
    public async Task<JsonResult> Transfer(GuardianBalanceTransferRequest request, CancellationToken cancellationToken)
    {
        var result = await _repository.TransferBalanceAsync(request, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<IActionResult> GetAddStudentView(int id, CancellationToken cancellationToken)
    {
        var model = await _repository.GetAddStudentViewAsync(id, cancellationToken);
        if (model is null)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">Parent was not found.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
        }

        ViewBag.Grades = model.Grades;
        ViewBag.Schools = model.Schools;
        var allergies = await _mealEnumRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        ViewBag.Allergies = new Microsoft.AspNetCore.Mvc.Rendering.SelectList(allergies, "Id", "Name");
        return PartialView("_AddStudent", new GuardianAddStudentRequest
        {
            GuardianId = model.GuardianId,
            Gender = "Male"
        });
    }

    [HttpPost]
    public async Task<JsonResult> AddStudent(GuardianAddStudentRequest request, CancellationToken cancellationToken)
    {
        var result = await _repository.AddStudentAsync(request, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<IActionResult> GetEditStudentView(int guardianId, decimal userId, CancellationToken cancellationToken)
    {
        var model = await _repository.GetEditStudentViewAsync(guardianId, userId, cancellationToken);
        if (model is null)
        {
            return Content("<div class=\"modal-body\"><p class=\"text-danger mb-0 p-4 h6\">Student was not found for this parent.</p></div><div class=\"modal-footer\"><button type=\"button\" class=\"btn btn-secondary\" data-bs-dismiss=\"modal\">Close</button></div>");
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
        var result = await _repository.EditStudentAsync(request, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }
}
