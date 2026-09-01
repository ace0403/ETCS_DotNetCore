using ETCS.Shared.Infrastructure.Email;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ETCS.Shared.Application.Email;

public sealed class GuardianEmailNotificationService : IGuardianEmailNotificationService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IEmailNotificationRepository _emailNotificationRepository;
    private readonly IOrderEmailContentBuilder _orderEmailContentBuilder;
    private readonly ParentPortalOptions _parentPortalOptions;
    private readonly ILogger<GuardianEmailNotificationService> _logger;

    public GuardianEmailNotificationService(
        IStudentRepository studentRepository,
        IEmailNotificationRepository emailNotificationRepository,
        IOrderEmailContentBuilder orderEmailContentBuilder,
        IOptions<ParentPortalOptions> parentPortalOptions,
        ILogger<GuardianEmailNotificationService> logger)
    {
        _studentRepository = studentRepository;
        _emailNotificationRepository = emailNotificationRepository;
        _orderEmailContentBuilder = orderEmailContentBuilder;
        _parentPortalOptions = parentPortalOptions.Value;
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

        if (await _emailNotificationRepository.ExistsForOrderAsync(templateKey, orderId, cancellationToken))
        {
            _logger.LogInformation(
                "Skipping email {TemplateKey}: notification already queued or sent for order {OrderId}.",
                templateKey,
                orderId);
            return;
        }

        var orderItems = await _orderEmailContentBuilder.BuildOrderSuccessContentAsync(
            guardianId,
            studentId,
            orderTypeId,
            orderId,
            total,
            cancellationToken);

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
                    LogoUrl = ResolvePortalLogoUrl(),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue password reset email for {Email}.", guardianEmail);
        }
    }

    public async Task QueueRegistrationOtpAsync(
        string email,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Skipping registration OTP email: email is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            _logger.LogWarning("Skipping registration OTP email: OTP code is empty.");
            return;
        }

        try
        {
            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = EmailTemplateKeys.RegistrationOtp,
                    ToEmail = email.Trim(),
                    GuardianName = "Guardian",
                    OtpCode = otpCode.Trim(),
                    ExpiryMinutes = expiryMinutes.ToString(CultureInfo.InvariantCulture),
                    LogoUrl = ResolvePortalLogoUrl(),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue registration OTP email for {Email}.", email);
            throw;
        }
    }

    public async Task QueueDeleteAccountOtpAsync(
        string email,
        string guardianName,
        string otpCode,
        int expiryMinutes,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Skipping delete-account OTP email: email is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(otpCode))
        {
            _logger.LogWarning("Skipping delete-account OTP email: OTP code is empty.");
            return;
        }

        try
        {
            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = EmailTemplateKeys.DeleteAccountOtp,
                    ToEmail = email.Trim(),
                    GuardianName = string.IsNullOrWhiteSpace(guardianName) ? "Guardian" : guardianName.Trim(),
                    OtpCode = otpCode.Trim(),
                    ExpiryMinutes = expiryMinutes.ToString(CultureInfo.InvariantCulture),
                    LogoUrl = ResolvePortalLogoUrl(),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue delete-account OTP email for {Email}.", email);
            throw;
        }
    }

    public async Task QueueRegistrationSuccessAsync(
        string email,
        string guardianName,
        string addChildLink,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogInformation("Skipping registration success email: email is empty.");
            return;
        }

        if (string.IsNullOrWhiteSpace(addChildLink))
        {
            _logger.LogWarning("Skipping registration success email: add-child link is empty.");
            return;
        }

        try
        {
            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = EmailTemplateKeys.RegistrationSuccess,
                    ToEmail = email.Trim(),
                    GuardianName = string.IsNullOrWhiteSpace(guardianName) ? "Guardian" : guardianName.Trim(),
                    AddChildLink = addChildLink.Trim(),
                    LogoUrl = ResolvePortalLogoUrl(addChildLink),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue registration success email for {Email}.", email);
        }
    }

    public async Task QueueReplaceCardRequestAsync(
        int guardianId,
        string customerId,
        string cardNumber,
        string reason,
        int? refCode,
        CancellationToken cancellationToken)
    {
        try
        {
            var template = await _emailNotificationRepository.GetTemplateByKeyAsync(
                EmailTemplateKeys.ReplaceCardRequest,
                cancellationToken);

            if (template is null || !template.IsActive)
            {
                _logger.LogWarning("Skipping replace-card email: template {TemplateKey} is missing or inactive.", EmailTemplateKeys.ReplaceCardRequest);
                return;
            }

            if (string.IsNullOrWhiteSpace(template.RecipientEmail))
            {
                _logger.LogWarning(
                    "Skipping replace-card email: RecipientEmail is not configured on template {TemplateKey}.",
                    EmailTemplateKeys.ReplaceCardRequest);
                return;
            }

            var guardian = await _studentRepository.GetGuardianBasicDetailByCustomerIdAsync(
                customerId,
                cancellationToken);
            var identity = await _studentRepository.GetStudentIdentityByCustomerIdAsync(
                customerId,
                cancellationToken);
            var students = await _studentRepository.GetStudentsByGuardianAsync(
                guardianId,
                customerId,
                cancellationToken);
            var student = students.FirstOrDefault();

            var guardianName = string.IsNullOrWhiteSpace(guardian?.GuardianName)
                ? "Guardian"
                : guardian.GuardianName.Trim();
            var studentName = !string.IsNullOrWhiteSpace(student?.Name)
                ? student.Name.Trim()
                : (identity?.StudentName.Trim() ?? "Student");
            var schoolName = student?.SchoolName?.Trim() ?? string.Empty;

            await _emailNotificationRepository.QueueAsync(
                new QueueEmailNotificationRequest
                {
                    TemplateKey = EmailTemplateKeys.ReplaceCardRequest,
                    GuardianId = guardianId,
                    GuardianName = guardianName,
                    StudentName = studentName,
                    CardNumber = cardNumber.Trim(),
                    CustomerId = customerId.Trim(),
                    Reason = reason.Trim(),
                    RefCode = refCode?.ToString(CultureInfo.InvariantCulture) ?? string.Empty,
                    SchoolName = schoolName,
                    LogoUrl = ResolvePortalLogoUrl(),
                    EventDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture)
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to queue replace-card email for guardian {GuardianId}, customer {CustomerId}.",
                guardianId,
                customerId);
        }
    }

    private string ResolvePortalLogoUrl(string? portalLink = null)
    {
        if (!string.IsNullOrWhiteSpace(portalLink)
            && Uri.TryCreate(portalLink.Trim(), UriKind.Absolute, out var fromLink))
        {
            return $"{fromLink.GetLeftPart(UriPartial.Authority).TrimEnd('/')}/images/logo.png";
        }

        var baseUrl = (_parentPortalOptions.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return string.Empty;
        }

        return $"{baseUrl}/images/logo.png";
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

        var template = await _emailNotificationRepository.GetTemplateByKeyAsync(templateKey, cancellationToken);
        if (template is null || !template.IsActive)
        {
            _logger.LogError(
                "Email template not found or inactive for {TemplateKey}. Order {OrderId} email was not queued.",
                templateKey,
                orderId);
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
                    LogoUrl = ResolvePortalLogoUrl(),
                    GuardianId = guardianId
                },
                cancellationToken);
        }
        catch (Exception ex) when (IsTemplateMissingException(ex))
        {
            _logger.LogError(
                ex,
                "Email template not found or inactive for {TemplateKey}. Order {OrderId} email was not queued.",
                templateKey,
                orderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue guardian email {TemplateKey} for order {OrderId}.", templateKey, orderId);
        }
    }

    private static bool IsTemplateMissingException(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("Email template not found or inactive", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
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
