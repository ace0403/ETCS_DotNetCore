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

    Task QueueRegistrationOtpAsync(
        string email,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken);

    Task QueueDeleteAccountOtpAsync(
        string email,
        string guardianName,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken);

    Task QueueRegistrationSuccessAsync(
        string email,
        string guardianName,
        string addChildLink,
        CancellationToken cancellationToken);

    Task QueueReplaceCardRequestAsync(
        int guardianId,
        string customerId,
        string cardNumber,
        string reason,
        int? refCode,
        CancellationToken cancellationToken);
}
