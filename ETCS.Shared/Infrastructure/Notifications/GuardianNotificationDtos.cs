namespace ETCS.Shared.Infrastructure.Notifications;

public static class GuardianNotificationTypes
{
    public const string TopupSuccess = "TopupSuccess";
    public const string OrderConfirmed = "OrderConfirmed";
    public const string OrderPending = "OrderPending";
    public const string System = "System";
    public const string Announcement = "Announcement";
}

public static class GuardianNotificationReferenceTypes
{
    public const string Topup = "Topup";
    public const string Order = "Order";
}

public sealed class GuardianNotificationDto
{
    public long Id { get; init; }

    public int GuardianId { get; init; }

    public int? StudentId { get; init; }

    public int? SchoolId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ReferenceType { get; init; }

    public string? ReferenceId { get; init; }

    public bool IsRead { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime? ReadOn { get; init; }

    public string? CreatedBy { get; init; }
}

public sealed class GuardianNotificationPageDto
{
    public IReadOnlyList<GuardianNotificationDto> Items { get; init; } = [];

    public int TotalCount { get; init; }

    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class CreateGuardianNotificationRequest
{
    public int GuardianId { get; init; }

    public int? StudentId { get; init; }

    public int? SchoolId { get; init; }

    public string Type { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? ReferenceType { get; init; }

    public string? ReferenceId { get; init; }

    public string CreatedBy { get; init; } = "System";
}

public sealed class CreateSchoolBroadcastNotificationRequest
{
    public int SchoolId { get; init; }

    public string Type { get; init; } = GuardianNotificationTypes.Announcement;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string CreatedBy { get; init; } = "Admin";
}
