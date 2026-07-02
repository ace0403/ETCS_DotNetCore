using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Email;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class EmailTemplateController : Controller
{
    private readonly IEmailNotificationRepository _repository;

    public EmailTemplateController(IEmailNotificationRepository repository)
    {
        _repository = repository;
    }

    public IActionResult Index() => View();

    public IActionResult Log() => View("NotificationLog");

    [HttpPost]
    public async Task<JsonResult> GetList([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        var templates = await _repository.GetTemplatesAsync(cancellationToken);
        var response = new DataTableResponse<EmailTemplateListDto>
        {
            Draw = request.Draw,
            RecordsTotal = templates.Count,
            RecordsFiltered = templates.Count,
            Data = templates.ToList()
        };
        return Json(response);
    }

    public async Task<IActionResult> Get(string templateKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
        {
            return BadRequest();
        }

        var model = await _repository.GetTemplateByKeyAsync(templateKey.Trim(), cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return PartialView("_AddUpdate", model);
    }

    [HttpPost]
    public async Task<JsonResult> Save(EmailTemplateSaveRequest model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.TemplateKey))
        {
            return Json(new { Success = false, Message = "Template key is required." });
        }

        if (!EmailTemplateKeys.SystemKeys.Contains(model.TemplateKey, StringComparer.OrdinalIgnoreCase))
        {
            return Json(new { Success = false, Message = "Unknown template key." });
        }

        if (string.IsNullOrWhiteSpace(model.SubjectTemplate))
        {
            return Json(new { Success = false, Message = "Subject is required." });
        }

        if (string.IsNullOrWhiteSpace(model.BodyHtmlTemplate))
        {
            return Json(new { Success = false, Message = "Email body is required." });
        }

        await _repository.SaveTemplateAsync(model, cancellationToken);
        return Json(new { Success = true, Message = "Email template saved." });
    }

    [HttpPost]
    public async Task<JsonResult> GetNotificationLog([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        var items = await _repository.GetLogAsync(200, cancellationToken);
        var response = new DataTableResponse<EmailNotificationLogDto>
        {
            Draw = request.Draw,
            RecordsTotal = items.Count,
            RecordsFiltered = items.Count,
            Data = items.ToList()
        };
        return Json(response);
    }
}
