namespace ETCS.Shared.Application.Email;

public interface IGuardianEmailNotificationService
{
    Task QueueTopupSuccessAsync(
        int studentId,
        int guardianId,
        string guardianEmail,
        string guardianName,
        string orderId,
        string transactionId,
        decimal amount,
        CancellationToken cancellationToken);

    Task QueueOrderSuccessAsync(
        int studentId,
        int guardianId,
        string guardianEmail,
        string guardianName,
        int orderTypeId,
        string orderId,
        string transactionId,
        decimal total,
        CancellationToken cancellationToken);

    Task QueuePasswordResetAsync(
        string guardianEmail,
        string guardianName,
        string resetLink,
        int expiryMinutes,
        CancellationToken cancellationToken);
}
