namespace ETCS.Shared.Options;

public sealed class RefreshTokenStoreOptions
{
    public const string SectionName = "RefreshTokenStore";

    /// <summary>Use <c>InMemory</c> (default) or <c>Sql</c> for durable multi-instance refresh tokens.</summary>
    public string Provider { get; set; } = "Sql";

    /// <summary>SQL table name when <see cref="Provider"/> is <c>Sql</c>. Created automatically if missing.</summary>
    public string TableName { get; set; } = "APIRefreshTokens";
}
