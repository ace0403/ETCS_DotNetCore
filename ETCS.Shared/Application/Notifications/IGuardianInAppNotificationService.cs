using ETCS.Shared.Infrastructure.Notifications;

namespace ETCS.Shared.Application.Notifications;

public interface IGuardianInAppNotificationService
{
    Task CreateTopupSuccessAsync(
        int studentId,
        int guardianId,
        decimal amount,
        string orderId,
        int? schoolId,
        CancellationToken cancellationToken);

    Task CreateOrderSuccessAsync(
        int studentId,
        int guardianId,
        string studentName,
        string orderId,
        string mealLabel,
        int? schoolId,
        CancellationToken cancellationToken);

    Task CreateAsync(CreateGuardianNotificationRequest request, CancellationToken cancellationToken);

    Task<int> BroadcastToSchoolAsync(CreateSchoolBroadcastNotificationRequest request, CancellationToken cancellationToken);
}
