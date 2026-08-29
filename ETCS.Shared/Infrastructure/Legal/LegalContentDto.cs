namespace ETCS.Shared.Infrastructure.Legal;

public sealed class LegalContentDto
{
    public string ContentKey { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string BodyHtml { get; init; } = string.Empty;

    public DateTime LastUpdatedOn { get; init; }
}
