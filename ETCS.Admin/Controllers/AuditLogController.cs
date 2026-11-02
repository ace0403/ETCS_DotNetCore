using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class AuditLogController : Controller
{
    private readonly IAuditLogQueryRepository _repository;

    public AuditLogController(IAuditLogQueryRepository repository)
    {
        _repository = repository;
    }

    public IActionResult Index() => View();

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] AuditLogSearchRequest request, CancellationToken cancellationToken)
    {
        var response = await _repository.SearchAsync(request, cancellationToken);
        return Json(response);
    }

    public async Task<IActionResult> Detail(long id, CancellationToken cancellationToken)
    {
        if (id <= 0)
        {
            return BadRequest();
        }

        var row = await _repository.GetAsync(id, cancellationToken);
        if (row is null)
        {
            return NotFound();
        }

        return Json(row);
    }
}
