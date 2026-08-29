using System.Security.Cryptography;
using System.Text;

namespace ETCS.Shared.Infrastructure.Legal;

public static class LegalContentCacheClearGuard
{
    public static bool IsAuthorized(string? providedKey, string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(configuredKey) || string.IsNullOrWhiteSpace(providedKey))
        {
            return false;
        }

        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey.Trim()));
        var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey.Trim()));

        return providedHash.Length == configuredHash.Length
            && CryptographicOperations.FixedTimeEquals(providedHash, configuredHash);
    }
}
