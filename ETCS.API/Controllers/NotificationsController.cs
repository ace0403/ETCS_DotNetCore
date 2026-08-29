using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Notifications;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/notifications")]
[Route("api/notifications")]
[Authorize]
public sealed class NotificationsController : ControllerBase
{
    private readonly IGuardianNotificationRepository _notificationRepository;

    public NotificationsController(IGuardianNotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? top = null,
        [FromQuery] bool unreadOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        // Legacy clients: `top` returns the first page with that size.
        if (top is > 0)
        {
            page = 1;
            pageSize = top.Value;
        }

        var result = await _notificationRepository.GetByGuardianPagedAsync(
            guardianId,
            page,
            pageSize,
            unreadOnly,
            cancellationToken);

        return Ok(new
        {
            items = result.Items,
            page = result.Page,
            pageSize = result.PageSize,
            totalCount = result.TotalCount,
            totalPages = result.TotalPages
        });
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var count = await _notificationRepository.GetUnreadCountAsync(guardianId, cancellationToken);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("{id:long}/read")]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var updated = await _notificationRepository.MarkReadAsync(guardianId, id, cancellationToken);
        return Ok(new { updated });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var updated = await _notificationRepository.MarkAllReadAsync(guardianId, cancellationToken);
        return Ok(new { updated });
    }
}
