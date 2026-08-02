using ETCS.Shared.Infrastructure.Notifications;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

[Authorize]
public sealed class NotificationsController : Controller
{
    private const int PageSize = 20;
    private const int DropdownSize = 5;

    private readonly IGuardianNotificationRepository _notificationRepository;

    public NotificationsController(IGuardianNotificationRepository notificationRepository)
    {
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int page = 1, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (page <= 0)
        {
            page = 1;
        }

        var paged = await _notificationRepository.GetByGuardianPagedAsync(
            guardianId,
            page,
            PageSize,
            unreadOnly: false,
            cancellationToken);

        if (paged.TotalPages > 0 && page > paged.TotalPages)
        {
            return RedirectToAction(nameof(Index), new { page = paged.TotalPages });
        }

        var unread = await _notificationRepository.GetUnreadCountAsync(guardianId, cancellationToken);

        var model = new NotificationsPageViewModel
        {
            UnreadCount = unread,
            Page = paged.Page,
            PageSize = paged.PageSize,
            TotalCount = paged.TotalCount,
            Items = paged.Items.Select(MapItem).ToList()
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Recent(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized();
        }

        var items = await _notificationRepository.GetByGuardianAsync(guardianId, DropdownSize, false, cancellationToken);
        var unread = await _notificationRepository.GetUnreadCountAsync(guardianId, cancellationToken);

        return Json(new
        {
            unreadCount = unread,
            items = items.Select(MapItem).ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> UnreadCount(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized();
        }

        var count = await _notificationRepository.GetUnreadCountAsync(guardianId, cancellationToken);
        return Json(new { unreadCount = count });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(long id, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized();
        }

        await _notificationRepository.MarkReadAsync(guardianId, id, cancellationToken);
        return Json(new { success = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized();
        }

        var updated = await _notificationRepository.MarkAllReadAsync(guardianId, cancellationToken);
        return Json(new { success = true, updated });
    }

    [HttpGet]
    public async Task<IActionResult> Open(long id, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var item = await _notificationRepository.GetByIdForGuardianAsync(guardianId, id, cancellationToken);
        if (item is null)
        {
            return RedirectToAction(nameof(Index));
        }

        await _notificationRepository.MarkReadAsync(guardianId, id, cancellationToken);

        if (string.Equals(item.ReferenceType, GuardianNotificationReferenceTypes.Topup, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.ReferenceId))
        {
            if (int.TryParse(item.ReferenceId, out var topupId))
            {
                return RedirectToAction("TopupDetail", "History", new { id = topupId });
            }

            return RedirectToAction("Index", "History", new { view = "transactions" });
        }

        if (string.Equals(item.ReferenceType, GuardianNotificationReferenceTypes.Order, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(item.ReferenceId))
        {
            return RedirectToAction("Detail", "History", new { orderId = item.ReferenceId });
        }

        return RedirectToAction(nameof(Index));
    }

    private NotificationListItemViewModel MapItem(GuardianNotificationDto item)
    {
        var (icon, tone) = ResolveVisual(item.Type);
        return new()
        {
            Id = item.Id,
            Title = item.Title,
            Message = item.Message,
            Type = item.Type,
            Icon = icon,
            ToneCss = tone,
            IsRead = item.IsRead,
            CreatedOn = item.CreatedOn,
            RelativeTime = FormatRelativeTime(item.CreatedOn),
            DetailUrl = Url.Action(nameof(Open), "Notifications", new { id = item.Id }) ?? "#"
        };
    }

    private static (string Icon, string ToneCss) ResolveVisual(string type) =>
        type switch
        {
            GuardianNotificationTypes.TopupSuccess => ("wallet.svg", "tone-topup"),
            GuardianNotificationTypes.OrderConfirmed => ("shopping.svg", "tone-order"),
            GuardianNotificationTypes.OrderPending => ("clock.svg", "tone-pending"),
            GuardianNotificationTypes.Announcement => ("notification.svg", "tone-announce"),
            _ => ("notification.svg", "tone-system")
        };

    private static string FormatRelativeTime(DateTime createdOn)
    {
        var local = createdOn.Kind == DateTimeKind.Utc ? createdOn.ToLocalTime() : createdOn;
        var span = DateTime.Now - local;

        if (span.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (span.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)span.TotalMinutes);
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }

        if (span.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)span.TotalHours);
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (span.TotalDays < 2)
        {
            return "Yesterday";
        }

        var days = (int)span.TotalDays;
        return days == 1 ? "1 day ago" : $"{days} days ago";
    }
}
