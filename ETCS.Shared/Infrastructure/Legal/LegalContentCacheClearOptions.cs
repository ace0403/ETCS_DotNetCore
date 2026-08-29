namespace ETCS.Shared.Infrastructure.Legal;

public sealed class LegalContentCacheClearOptions
{
    public const string SectionName = "LegalContent";

    public const string HeaderName = "X-Cache-Clear-Key";

    public string CacheClearKey { get; set; } = string.Empty;
}
