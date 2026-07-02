using System.Security.Cryptography;
using System.Text;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.API.Infrastructure.Auth;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ApiKeyOptions _options;
    private readonly PosOptions _posOptions;
    private readonly byte[][] _configuredKeyHashes;

    public ApiKeyMiddleware(
        RequestDelegate next,
        IOptions<ApiKeyOptions> options,
        IOptions<PosOptions> posOptions)
    {
        _next = next;
        _options = options.Value;
        _posOptions = posOptions.Value;
        _configuredKeyHashes = BuildKeyHashes(_options.Keys, _posOptions.ApiKey);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (HttpMethods.IsOptions(context.Request.Method))
        {
            await _next(context);
            return;
        }

        if (!RequiresApiKey(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (_configuredKeyHashes.Length == 0)
        {
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new { message = "API key validation is not configured." });
            return;
        }

        if (!TryGetProvidedKey(context, out var providedKey) || !IsValidKey(providedKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or missing API key." });
            return;
        }

        await _next(context);
    }

    private static bool RequiresApiKey(PathString path)
    {
        if (!path.HasValue)
        {
            return false;
        }

        var value = path.Value!;
        return value.StartsWith("/api/Auth", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/auth", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/pos", StringComparison.OrdinalIgnoreCase);
    }

    private static byte[][] BuildKeyHashes(IEnumerable<string> apiKeys, string? posApiKey)
    {
        var keys = apiKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k.Trim())
            .ToList();

        if (!string.IsNullOrWhiteSpace(posApiKey))
        {
            keys.Add(posApiKey.Trim());
        }

        return keys
            .Distinct(StringComparer.Ordinal)
            .Select(k => SHA256.HashData(Encoding.UTF8.GetBytes(k)))
            .ToArray();
    }

    private bool TryGetProvidedKey(HttpContext context, out string key)
    {
        key = string.Empty;
        if (!context.Request.Headers.TryGetValue(_options.HeaderName, out var values))
        {
            foreach (var header in context.Request.Headers)
            {
                if (string.Equals(header.Key, _options.HeaderName, StringComparison.OrdinalIgnoreCase))
                {
                    key = header.Value.ToString();
                    return !string.IsNullOrWhiteSpace(key);
                }
            }

            return false;
        }

        key = values.ToString();
        return !string.IsNullOrWhiteSpace(key);
    }

    private bool IsValidKey(string providedKey)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(providedKey.Trim()));
        foreach (var configuredHash in _configuredKeyHashes)
        {
            if (providedHash.Length == configuredHash.Length &&
                CryptographicOperations.FixedTimeEquals(providedHash, configuredHash))
            {
                return true;
            }
        }

        return false;
    }
}
