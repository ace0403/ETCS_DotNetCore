using System.ComponentModel.DataAnnotations;
using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public sealed class InAppNotificationController : Controller
{
    private readonly IGuardianNotificationRepository _notificationRepository;
    private readonly IGuardianInAppNotificationService _notificationService;
    private readonly ISchoolAdminRepository _schoolAdminRepository;

    public InAppNotificationController(
        IGuardianNotificationRepository notificationRepository,
        IGuardianInAppNotificationService notificationService,
        ISchoolAdminRepository schoolAdminRepository)
    {
        _notificationRepository = notificationRepository;
        _notificationService = notificationService;
        _schoolAdminRepository = schoolAdminRepository;
    }

    [Route("notification")]
    public IActionResult Index() => View();

    public async Task<IActionResult> Broadcast(CancellationToken cancellationToken)
    {
        var schools = await _schoolAdminRepository.GetDataAsync(
            new DataTableRequest { Start = 0, Length = 500, Draw = 1 },
            cancellationToken);
        ViewBag.Schools = schools.Data;
        return View(new InAppNotificationBroadcastRequest());
    }

    [HttpPost]
    public async Task<JsonResult> GetLog([FromForm] DataTableRequest request, CancellationToken cancellationToken)
    {
        var items = await _notificationRepository.GetAdminLogAsync(200, null, null, null, cancellationToken);
        var response = new DataTableResponse<GuardianNotificationDto>
        {
            Draw = request.Draw,
            RecordsTotal = items.Count,
            RecordsFiltered = items.Count,
            Data = items.ToList()
        };
        return Json(response);
    }

    [HttpPost]
    public async Task<JsonResult> SendBroadcast(InAppNotificationBroadcastRequest model, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(model.Title))
        {
            return Json(new { Success = false, Message = "Title is required." });
        }

        if (string.IsNullOrWhiteSpace(model.Message))
        {
            return Json(new { Success = false, Message = "Message is required." });
        }

        var createdBy = User.Identity?.Name ?? "Admin";
        var type = string.IsNullOrWhiteSpace(model.Type)
            ? GuardianNotificationTypes.Announcement
            : model.Type.Trim();

        if (model.GuardianId is > 0)
        {
            await _notificationService.CreateAsync(
                new CreateGuardianNotificationRequest
                {
                    GuardianId = model.GuardianId.Value,
                    SchoolId = model.SchoolId > 0 ? model.SchoolId : null,
                    Type = type,
                    Title = model.Title.Trim(),
                    Message = model.Message.Trim(),
                    CreatedBy = createdBy
                },
                cancellationToken);

            return Json(new { Success = true, Message = "Notification sent to guardian." });
        }

        if (model.SchoolId <= 0)
        {
            return Json(new { Success = false, Message = "Select a school or enter a guardian id." });
        }

        var inserted = await _notificationService.BroadcastToSchoolAsync(
            new CreateSchoolBroadcastNotificationRequest
            {
                SchoolId = model.SchoolId,
                Type = type,
                Title = model.Title.Trim(),
                Message = model.Message.Trim(),
                CreatedBy = createdBy
            },
            cancellationToken);

        return Json(new
        {
            Success = true,
            Message = inserted > 0
                ? $"Notification sent to {inserted} guardian(s)."
                : "No guardians found for the selected school."
        });
    }
}

public sealed class InAppNotificationBroadcastRequest
{
    [Display(Name = "School")]
    public int SchoolId { get; set; }

    [Display(Name = "Guardian Id (optional)")]
    public int? GuardianId { get; set; }

    public string Type { get; set; } = GuardianNotificationTypes.Announcement;

    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(1000)]
    public string Message { get; set; } = string.Empty;
}
