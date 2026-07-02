namespace ETCS.Shared.Options;

public sealed class ApiKeyOptions
{
    public const string SectionName = "ApiKey";

    public string HeaderName { get; set; } = "X-API-KEY";

    public string[] Keys { get; set; } = [];
}
