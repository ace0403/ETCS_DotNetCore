namespace ETCS.Shared.Infrastructure.Email;

public sealed class SmtpSettingsDto
{
    public int Id { get; init; }

    public string SmtpHost { get; init; } = string.Empty;

    public string SmtpEmail { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;

    public bool Ssl { get; init; }

    public int Port { get; init; }
}

public sealed class SmtpSettingsSaveRequest
{
    public int Id { get; set; }

    public string SmtpHost { get; set; } = string.Empty;

    public string SmtpEmail { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public bool Ssl { get; set; } = true;

    public int Port { get; set; } = 587;
}

public sealed class PendingEmailNotificationDto
{
    public long Id { get; init; }

    public string TemplateKey { get; init; } = string.Empty;

    public string ToEmail { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string BodyHtml { get; init; } = string.Empty;

    public string? PayloadJson { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }
}

public sealed class EmailTemplateListDto
{
    public int Id { get; init; }

    public string TemplateKey { get; init; } = string.Empty;

    public string SubjectTemplate { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime? UpdatedOn { get; init; }
}

public sealed class EmailTemplateDetailDto
{
    public int Id { get; init; }

    public string TemplateKey { get; init; } = string.Empty;

    public string SubjectTemplate { get; init; } = string.Empty;

    public string BodyHtmlTemplate { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime? UpdatedOn { get; init; }
}

public sealed class EmailTemplateSaveRequest
{
    public string TemplateKey { get; set; } = string.Empty;

    public string SubjectTemplate { get; set; } = string.Empty;

    public string BodyHtmlTemplate { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}

public sealed class EmailNotificationLogDto
{
    public long Id { get; init; }

    public string TemplateKey { get; init; } = string.Empty;

    public string ToEmail { get; init; } = string.Empty;

    public string Subject { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public string? ErrorMessage { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime? SentOn { get; init; }
}
