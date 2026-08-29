using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealTypes;
using ETCS.Shared.Infrastructure.Admin.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class MealTypeController : Controller
{
    private readonly IMealTypeAdminRepository _repository;

    public MealTypeController(IMealTypeAdminRepository repository)
    {
        _repository = repository;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        ViewBag.Sessions = await _repository.ListSessionsAsync(cancellationToken: cancellationToken);
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> GetList(
        [FromForm] DataTableRequest request,
        [FromForm] string kind,
        [FromForm] int? sessionId,
        CancellationToken cancellationToken)
    {
        if (MealTypeKinds.IsType(kind))
        {
            var types = await _repository.GetTypeDataAsync(request, sessionId, cancellationToken);
            return Json(types);
        }

        var sessions = await _repository.GetSessionDataAsync(request, cancellationToken);
        return Json(sessions);
    }

    public async Task<IActionResult> Get(int id, string kind, CancellationToken cancellationToken)
    {
        var resolvedKind = MealTypeKinds.IsType(kind) ? MealTypeKinds.Type : MealTypeKinds.Session;
        var model = id > 0
            ? await _repository.GetAsync(id, resolvedKind, cancellationToken)
                ?? new MealTypeSaveRequest { Kind = resolvedKind }
            : new MealTypeSaveRequest { Kind = resolvedKind };

        if (MealTypeKinds.IsType(resolvedKind))
        {
            ViewBag.Sessions = await _repository.ListSessionsAsync(
                activeOnly: true,
                includeSessionId: model.ParentId,
                cancellationToken);
            return PartialView("_AddUpdateType", model);
        }

        return PartialView("_AddUpdateSession", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(MealTypeSaveRequest model, CancellationToken cancellationToken)
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

    public async Task<JsonResult> Delete(int id, string kind, CancellationToken cancellationToken)
    {
        var result = await _repository.DeleteAsync(id, kind, cancellationToken);
        return Json(new { Success = result.Success, Message = result.Message });
    }
}
