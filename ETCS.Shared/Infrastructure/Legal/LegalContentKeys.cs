namespace ETCS.Shared.Infrastructure.Legal;

public static class LegalContentKeys
{
    public const string Privacy = "Privacy";
    public const string Terms = "Terms";
    public const string Cancellation = "Cancellation";

    public const string CacheKeyAll = "legal:all";

    public static readonly IReadOnlyList<string> All =
    [
        Privacy,
        Terms,
        Cancellation
    ];

    public static string CacheKeyFor(string contentKey) => $"legal:{contentKey}";

    public static bool IsKnown(string contentKey) =>
        All.Any(k => string.Equals(k, contentKey, StringComparison.OrdinalIgnoreCase));

    public static string? Normalize(string contentKey)
    {
        foreach (var key in All)
        {
            if (string.Equals(key, contentKey, StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }

        return null;
    }
}
