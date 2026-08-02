using ETCS.Shared.Infrastructure.Email;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.Extensions.Logging;
using System.Globalization;

namespace ETCS.Shared.Application.Email;

public sealed class GuardianEmailNotificationService : IGuardianEmailNotificationService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IEmailNotificationRepository _emailNotificationRepository;
    private readonly ILogger<GuardianEmailNotificationService> _logger;

    public GuardianEmailNotificationService(
        IStudentRepository studentRepository,
        IMealOrderRepository mealOrderRepository,
        IEmailNotificationRepository emailNotificationRepository,
        ILogger<GuardianEmailNotificationService> logger)
    {
        _studentRepository = studentRepository;
        _mealOrderRepository = mealOrderRepository;
        _emailNotificationRepository = emailNotificationRepository;
        _logger = logger;
    }

    public Task QueueTopupSuccessAsync(
        int studentId,
        int guardianId,
        string guardianEmail,
        string guardianName,
        string orderId,
        string transactionId,
        decimal amount,
        CancellationToken cancellationToken) =>
        QueueAsync(
            EmailTemplateKeys.TopupSuccess,
            studentId,
            guardianId,
            guardianEmail,
            guardianName,
            orderId,
            transactionId,
            amount,
            orderItems: string.Empty,
            cancellationToken);

    public async Task QueueOrderSuccessAsync(
        int studentId,
        int guardianId,
        string guardianEmail,
        string guardianName,
        int orderTypeId,
        string orderId,
        string transactionId,
        decimal total,
        CancellationToken cancellationToken)
    {
        var templateKey = EmailTemplateKeyResolver.ResolveForOrderType(orderTypeId);
        var orderDetail = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(
            guardianId,
            orderId,
            cancellationToken);
        var orderItems = OrderEmailHelper.BuildOrderItemsHtml(orderDetail?.LineItems);

        await QueueAsync(
            templateKey,
            studentId,
            guardianId,
            guardianEmail,
            guardianName,
            orderId,
            transactionId,
            total,
            orderItems,
            cancellationToken);
    }

    public async Task QueuePasswordResetAsync(
        string guardianEmail,
        string guardianName,
        string resetLink,
        int expiryMinutes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(guardianEmail))
        {
            _logger.LogInformation("Skipping password reset email: guardian email is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(resetLink))
        {
            _logger.LogWarning("Skipping password reset email: reset link is empty.");
            return;
        }

        try
        {
            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = EmailTemplateKeys.PasswordReset,
                    ToEmail = guardianEmail.Trim(),
                    GuardianName = string.IsNullOrWhiteSpace(guardianName) ? "Guardian" : guardianName.Trim(),
                    ResetLink = resetLink.Trim(),
                    ExpiryMinutes = expiryMinutes.ToString(CultureInfo.InvariantCulture),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue password reset email for {Email}.", guardianEmail);
        }
    }

    private async Task QueueAsync(
        string templateKey,
        int studentId,
        int guardianId,
        string guardianEmail,
        string guardianName,
        string orderId,
        string transactionId,
        decimal amount,
        string orderItems,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(guardianEmail))
        {
            _logger.LogInformation("Skipping email {TemplateKey}: guardian email is empty for student {StudentId}.", templateKey, studentId);
            return;
        }

        if (!await IsSchoolEmailEnabledAsync(studentId, cancellationToken))
        {
            _logger.LogInformation("Skipping email {TemplateKey}: school email alerts disabled for student {StudentId}.", templateKey, studentId);
            return;
        }

        var studentName = await ResolveStudentNameAsync(studentId, guardianId, cancellationToken);
        var eventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture);

        try
        {
            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = templateKey,
                    ToEmail = guardianEmail.Trim(),
                    GuardianName = guardianName.Trim(),
                    StudentName = studentName,
                    OrderId = orderId,
                    TransactionId = transactionId,
                    Amount = amount.ToString("F2", CultureInfo.InvariantCulture),
                    EventDate = eventDate,
                    OrderItems = orderItems,
                    GuardianId = guardianId
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue guardian email {TemplateKey} for order {OrderId}.", templateKey, orderId);
        }
    }

    private async Task<bool> IsSchoolEmailEnabledAsync(int studentId, CancellationToken cancellationToken)
    {
        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return true;
        }

        return await _studentRepository.GetSchoolEmailAlertsEnabledAsync(schoolId.Value, cancellationToken) ?? true;
    }

    private async Task<string> ResolveStudentNameAsync(int studentId, int guardianId, CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        var studentIdText = studentId.ToString(CultureInfo.InvariantCulture);
        var match = students.FirstOrDefault(s => s.UserId == studentId);

        return string.IsNullOrWhiteSpace(match?.Name)
            ? studentIdText
            : match.Name.Trim();
    }
}
