using System.Net;
using System.Net.Mail;
using ETCS.Shared.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Infrastructure.Email;

/// <summary>
/// Polls MealDB EmailNotification (Queued) and delivers via SmtpSettings.
/// Hosted by ETCS.EmailWorker (Windows Service). Do not host on ETCS.API —
/// API deploys/recycles would stop email delivery.
/// </summary>
public sealed class EmailDeliveryBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SmtpOptions _deliveryOptions;
    private readonly ILogger<EmailDeliveryBackgroundService> _logger;
    private bool _smtpMissingLogged;

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
        var pollSeconds = Math.Max(5, _deliveryOptions.PollIntervalSeconds);
        var batchSize = Math.Max(1, _deliveryOptions.BatchSize);
        var delay = TimeSpan.FromSeconds(pollSeconds);

        _logger.LogInformation(
            "Email delivery worker started. PollIntervalSeconds={PollIntervalSeconds}; BatchSize={BatchSize}; SendTimeoutSeconds={SendTimeoutSeconds}",
            pollSeconds,
            batchSize,
            Math.Max(5, _deliveryOptions.SendTimeoutSeconds));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(batchSize, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email delivery batch failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task ProcessBatchAsync(int batchSize, CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<IEmailNotificationRepository>();
        var smtpSettings = await repository.GetSmtpSettingsAsync(cancellationToken);

        if (!IsSmtpConfigured(smtpSettings))
        {
            if (!_smtpMissingLogged)
            {
                _logger.LogWarning(
                    "Email delivery skipped: MealDB dbo.SmtpSettings is missing or SmtpHost/SmtpEmail is empty. Queued emails will remain Queued until SMTP is configured. Confirm live ETCS.API MealDatabase connection string points at the same MealDB you are inspecting.");
                _smtpMissingLogged = true;
            }

            return;
        }

        if (smtpSettings!.Port <= 0)
        {
            if (!_smtpMissingLogged)
            {
                _logger.LogWarning(
                    "Email delivery skipped: MealDB dbo.SmtpSettings.Port is invalid ({Port}). Queued emails will remain Queued.",
                    smtpSettings.Port);
                _smtpMissingLogged = true;
            }

            return;
        }

        _smtpMissingLogged = false;

        IReadOnlyList<PendingEmailNotificationDto> pending;
        try
        {
            pending = await repository.GetPendingAsync(batchSize, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "spGetPendingEmailNotifications failed. Deploy email-notifications / email-delivery-claim-pending.sql on live MealDB. Queued rows will not move until this succeeds.");
            throw;
        }

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogInformation("Email delivery processing {Count} notification(s).", pending.Count);

        foreach (var item in pending)
        {
            try
            {
                await SendAsync(smtpSettings, item.ToEmail, item.Subject, item.BodyHtml, cancellationToken);
                await TryMarkStatusAsync(repository, item.Id, "Sent", null, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // App shutting down mid-send: leave Sending so reclaim or next cycle can finish.
                throw;
            }
            catch (Exception ex)
            {
                var message = ex.Message.Length > 2000 ? ex.Message[..2000] : ex.Message;
                _logger.LogWarning(ex, "Failed to send email notification {Id} to {ToEmail}.", item.Id, item.ToEmail);
                await TryMarkStatusAsync(repository, item.Id, "Failed", message, cancellationToken);
            }
        }
    }

    private async Task TryMarkStatusAsync(
        IEmailNotificationRepository repository,
        long id,
        string status,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            await repository.MarkStatusAsync(id, status, errorMessage, cancellationToken);
            _logger.LogInformation("Email notification {Id} marked {Status}.", id, status);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Email send outcome could not be persisted for notification {Id} (intended Status={Status}). Row may stay Queued/Sending.",
                id,
                status);
        }
    }

    private static bool IsSmtpConfigured(SmtpSettingsDto? settings) =>
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
        var sendTimeoutSeconds = Math.Max(5, _deliveryOptions.SendTimeoutSeconds);
        var timeoutMs = checked(sendTimeoutSeconds * 1000);

        using var client = new SmtpClient(smtpSettings.SmtpHost.Trim(), smtpSettings.Port)
        {
            EnableSsl = smtpSettings.Ssl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            UseDefaultCredentials = false,
            Timeout = timeoutMs,
            Credentials = new NetworkCredential(smtpSettings.SmtpEmail.Trim(), smtpSettings.Password ?? string.Empty)
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

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(sendTimeoutSeconds));

        try
        {
            await client.SendMailAsync(message, timeoutCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                client.SendAsyncCancel();
            }
            catch
            {
                // Best-effort cancel of a hung SMTP operation.
            }

            throw new TimeoutException($"SMTP send timed out after {sendTimeoutSeconds} seconds.");
        }
    }
}
