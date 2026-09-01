using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Infrastructure.Email;

public sealed class EmailNotificationRepository : IEmailNotificationRepository
{
    private const int DefaultCommandTimeoutSeconds = 30;
    private const string QueueSp = "spQueueEmailNotification";
    private const string MarkStatusSp = "spMarkEmailNotificationStatus";
    private const string GetTemplatesSp = "spGetEmailTemplates";
    private const string GetTemplateByKeySp = "spGetEmailTemplateByKey";
    private const string GetPendingSp = "spGetPendingEmailNotifications";
    private const string GetLogSp = "spGetEmailNotificationLog";
    private const string ExistsForOrderSp = "spEmailNotificationExistsForOrder";
    private const string UpsertTemplateSp = "spUpsertEmailTemplate";
    private const string GetSmtpSettingsSp = "spGetSmtpSettings";
    private const string UpsertSmtpSettingsSp = "spUpsertSmtpSettings";

    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly ITransactionRepository _transactionRepository;

    public EmailNotificationRepository(
        IMealDbConnectionFactory mealDbConnectionFactory,
        ITransactionRepository transactionRepository)
    {
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _transactionRepository = transactionRepository;
    }

    public Task QueueAsync(QueueEmailNotificationRequest request, CancellationToken cancellationToken) =>
        _transactionRepository.QueueEmailNotificationAsync(request, cancellationToken);

    public async Task<IReadOnlyList<PendingEmailNotificationDto>> GetPendingAsync(
        int batchSize,
        CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<PendingEmailNotificationDto>(
            new CommandDefinition(
                GetPendingSp,
                new { BatchSize = batchSize },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task MarkStatusAsync(long id, string status, string? errorMessage, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                MarkStatusSp,
                new { Id = id, Status = status, ErrorMessage = errorMessage },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EmailTemplateListDto>> GetTemplatesAsync(CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<EmailTemplateListDto>(
            new CommandDefinition(
                GetTemplatesSp,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<EmailTemplateDetailDto?> GetTemplateByKeyAsync(string templateKey, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QuerySingleOrDefaultAsync<EmailTemplateDetailDto>(
            new CommandDefinition(
                GetTemplateByKeySp,
                new { TemplateKey = templateKey.Trim() },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task SaveTemplateAsync(EmailTemplateSaveRequest request, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpsertTemplateSp,
                new
                {
                    TemplateKey = request.TemplateKey.Trim(),
                    SubjectTemplate = request.SubjectTemplate.Trim(),
                    BodyHtmlTemplate = request.BodyHtmlTemplate,
                    RecipientEmail = string.IsNullOrWhiteSpace(request.RecipientEmail)
                        ? string.Empty
                        : request.RecipientEmail.Trim(),
                    IsActive = request.IsActive
                },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<EmailNotificationLogDto>> GetLogAsync(int top, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<EmailNotificationLogDto>(
            new CommandDefinition(
                GetLogSp,
                new { Top = top },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    public async Task<bool> ExistsForOrderAsync(string templateKey, string orderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(templateKey) || string.IsNullOrWhiteSpace(orderId))
        {
            return false;
        }

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                ExistsForOrderSp,
                new { TemplateKey = templateKey.Trim(), OrderId = orderId.Trim() },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task<SmtpSettingsDto?> GetSmtpSettingsAsync(CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QuerySingleOrDefaultAsync<SmtpSettingsDto>(
            new CommandDefinition(
                GetSmtpSettingsSp,
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }

    public async Task SaveSmtpSettingsAsync(SmtpSettingsSaveRequest request, CancellationToken cancellationToken)
    {
        using var connection = _mealDbConnectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpsertSmtpSettingsSp,
                new
                {
                    Id = request.Id > 0 ? request.Id : (int?)null,
                    request.SmtpHost,
                    request.SmtpEmail,
                    request.Password,
                    SSL = request.Ssl,
                    request.Port
                },
                commandType: System.Data.CommandType.StoredProcedure,
                commandTimeout: DefaultCommandTimeoutSeconds,
                cancellationToken: cancellationToken));
    }
}
