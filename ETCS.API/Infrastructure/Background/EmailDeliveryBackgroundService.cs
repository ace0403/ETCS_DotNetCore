using System.Net;
using System.Net.Mail;
using ETCS.Shared.Infrastructure.Email;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.API.Infrastructure.Background;

public sealed class EmailDeliveryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SmtpOptions _deliveryOptions;
    private readonly ILogger<EmailDeliveryBackgroundService> _logger;

    public EmailDeliveryBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<SmtpOptions> deliveryOptions,
        ILogger<EmailDeliveryBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _deliveryOptions = deliveryOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var delay = TimeSpan.FromSeconds(Math.Max(5, _deliveryOptions.PollIntervalSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email delivery batch failed.");
            }

            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailNotificationRepository>();
        var smtpSettings = await repository.GetSmtpSettingsAsync(cancellationToken);

        if (!IsSmtpConfigured(smtpSettings))
        {
            return;
        }

        var pending = await repository.GetPendingAsync(_deliveryOptions.BatchSize, cancellationToken);

        foreach (var item in pending)
        {
            try
            {
                await SendAsync(smtpSettings!, item.ToEmail, item.Subject, item.BodyHtml, cancellationToken);
                await repository.MarkStatusAsync(item.Id, "Sent", null, cancellationToken);
            }
            catch (Exception ex)
            {
                var message = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                _logger.LogWarning(ex, "Failed to send email notification {Id} to {ToEmail}.", item.Id, item.ToEmail);
                await repository.MarkStatusAsync(item.Id, "Failed", message, cancellationToken);
            }
        }
    }

    private bool IsSmtpConfigured(SmtpSettingsDto? settings) =>
        settings is not null
        && !string.IsNullOrWhiteSpace(settings.SmtpHost)
        && !string.IsNullOrWhiteSpace(settings.SmtpEmail);

    private async Task SendAsync(
        SmtpSettingsDto smtpSettings,
        string toEmail,
        string subject,
        string bodyHtml,
        CancellationToken cancellationToken)
    {
        var port = smtpSettings.Port == 465 ? 587 : smtpSettings.Port;

        using var client = new SmtpClient(smtpSettings.SmtpHost.Trim(), port)
        {
            EnableSsl = smtpSettings.Ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Credentials = new NetworkCredential(smtpSettings.SmtpEmail.Trim(), smtpSettings.Password)
        };

        using var message = new MailMessage
        {
            From = new MailAddress(smtpSettings.SmtpEmail.Trim(), _deliveryOptions.DefaultFromName),
            Subject = subject,
            Body = bodyHtml,
            IsBodyHtml = true
        };

        foreach (var address in toEmail.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            message.To.Add(address);
        }

        await client.SendMailAsync(message, cancellationToken);
    }
}
