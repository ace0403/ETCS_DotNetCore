using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Infrastructure.Email;

public interface IEmailNotificationRepository
{
    Task QueueAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<PendingEmailNotificationDto>> GetPendingAsync(int batchSize, CancellationToken cancellationToken);

    Task MarkStatusAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailTemplateListDto>> GetTemplatesAsync(CancellationToken cancellationToken);

    Task<EmailTemplateDetailDto?> GetTemplateByKeyAsync(string templateKey, CancellationToken cancellationToken);

    Task SaveTemplateAsync(EmailTemplateSaveRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<EmailNotificationLogDto>> GetLogAsync(int top, CancellationToken cancellationToken);

    Task<bool> ExistsForOrderAsync(string templateKey, string orderId, CancellationToken cancellationToken);

    Task<SmtpSettingsDto?> GetSmtpSettingsAsync(CancellationToken cancellationToken);

    Task SaveSmtpSettingsAsync(SmtpSettingsSaveRequest request, CancellationToken cancellationToken);
}
