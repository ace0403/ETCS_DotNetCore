namespace ETCS.Web.Models;

public sealed class NotificationsPageViewModel
{
    public int UnreadCount { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public IReadOnlyList<NotificationListItemViewModel> Items { get; init; } = [];

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class NotificationListItemViewModel
{
    public long Id { get; init; }

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string Type { get; init; } = string.Empty;

    public string Icon { get; init; } = "notification.svg";

    public string ToneCss { get; init; } = "tone-system";

    public bool IsRead { get; init; }

    public DateTime CreatedOn { get; init; }

    public string RelativeTime { get; init; } = string.Empty;

    public string DetailUrl { get; init; } = "#";
}
