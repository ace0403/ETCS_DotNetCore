namespace ETCS.Shared.Infrastructure.Notifications;

public interface IGuardianNotificationRepository
{
    Task<long> CreateAsync(CreateGuardianNotificationRequest request, CancellationToken cancellationToken);

    Task<int> CreateForSchoolAsync(CreateSchoolBroadcastNotificationRequest request, CancellationToken cancellationToken);

    Task<GuardianNotificationPageDto> GetByGuardianPagedAsync(
        int guardianId,
        int page,
        int pageSize,
        bool unreadOnly,
        CancellationToken cancellationToken);

    /// <summary>Convenience for recent/top-N lists (page 1).</summary>
    Task<IReadOnlyList<GuardianNotificationDto>> GetByGuardianAsync(
        int guardianId,
        int top,
        bool unreadOnly,
        CancellationToken cancellationToken);

    Task<GuardianNotificationDto?> GetByIdForGuardianAsync(
        int guardianId,
        long notificationId,
        CancellationToken cancellationToken);

    Task<int> GetUnreadCountAsync(int guardianId, CancellationToken cancellationToken);

    Task<int> MarkReadAsync(int guardianId, long notificationId, CancellationToken cancellationToken);

    Task<int> MarkAllReadAsync(int guardianId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GuardianNotificationDto>> GetAdminLogAsync(
        int top,
        int? guardianId,
        int? schoolId,
        string? type,
        CancellationToken cancellationToken);
}
