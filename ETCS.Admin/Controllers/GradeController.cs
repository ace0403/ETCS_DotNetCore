using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Grades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class GradeController : Controller
{
    private readonly IGradeAdminRepository _repository;

    public GradeController(IGradeAdminRepository repository)
    {
        _repository = repository;
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
            ? await _repository.GetAsync(id, cancellationToken) ?? new GradeSaveRequest()
            : new GradeSaveRequest();

        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(GradeSaveRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { Success = false, Message = "Required fields are missing." });
        }

        var result = await _repository.SaveAsync(model, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }

    public async Task<JsonResult> Delete(int id, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }
}
