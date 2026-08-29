using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Media;

public sealed class MealImageUrlBuilder
{
    private readonly MediaOptions _options;

    public MealImageUrlBuilder(IOptions<MediaOptions> options)
    {
        _options = options.Value;
    }

    public static string? NormalizeFileName(string? imageName)
    {
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return null;
        }

        if (TryGetPassthroughImagePath(imageName, out _))
        {
            return null;
        }

        return Path.GetFileName(imageName.Trim());
    }

    public static bool TryGetPassthroughImagePath(string? imageName, out string path)
    {
        path = string.Empty;
        if (string.IsNullOrWhiteSpace(imageName))
        {
            return false;
        }

        var trimmed = imageName.Trim();
        if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith("//", StringComparison.Ordinal))
        {
            path = trimmed;
            return true;
        }

        if (trimmed.StartsWith('/'))
        {
            path = trimmed;
            return true;
        }

        return false;
    }

    public string GetFullImagePath(MealImageKind kind, string? imageName)
    {
        if (TryGetPassthroughImagePath(imageName, out var passthrough))
        {
            return passthrough;
        }

        var fileName = NormalizeFileName(imageName);
        if (fileName is null)
        {
            return MealImagePaths.GetDefaultThumbnailPath(kind);
        }

        if (TryResolveStoredPath(kind, fileName, thumbnail: false, out var requestPath))
        {
            return requestPath;
        }

        var folder = MealImagePaths.GetFolder(kind);
        return $"/{folder}/{fileName}";
    }

    public string GetThumbnailPath(MealImageKind kind, string? imageName, bool forPos = false)
    {
        if (TryGetPassthroughImagePath(imageName, out var passthrough))
        {
            return passthrough;
        }

        var fileName = NormalizeFileName(imageName);
        if (fileName is null)
        {
            return MealImagePaths.GetDefaultThumbnailPath(kind, forPos);
        }

        if (TryResolveStoredPath(kind, fileName, thumbnail: true, out var requestPath))
        {
            return requestPath;
        }

        var folder = MealImagePaths.GetFolder(kind);
        return $"/{folder}/{MealImagePaths.ThumbSubFolder}/{fileName}";
    }

    public string? GetFullImageUrl(MealImageKind kind, string? imageName, bool absolute = false)
    {
        if (TryGetPassthroughImagePath(imageName, out var passthrough))
        {
            return absolute && !passthrough.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? CombineBaseUrl(passthrough)
                : passthrough;
        }

        var fileName = NormalizeFileName(imageName);
        if (fileName is null)
        {
            return absolute ? CombineBaseUrl(MealImagePaths.GetDefaultThumbnailPath(kind)) : MealImagePaths.GetDefaultThumbnailPath(kind);
        }

        var path = GetFullImagePath(kind, fileName);
        return absolute ? CombineBaseUrl(path) : path;
    }

    public string? GetThumbnailUrl(MealImageKind kind, string? imageName, bool absolute = false, bool forPos = false)
    {
        if (TryGetPassthroughImagePath(imageName, out var passthrough))
        {
            return absolute && !passthrough.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                ? CombineBaseUrl(passthrough)
                : passthrough;
        }

        var fileName = NormalizeFileName(imageName);
        if (fileName is null)
        {
            var defaultPath = MealImagePaths.GetDefaultThumbnailPath(kind, forPos);
            return absolute ? CombineBaseUrl(defaultPath) : defaultPath;
        }

        var path = GetThumbnailPath(kind, fileName, forPos);
        return absolute ? CombineBaseUrl(path) : path;
    }

    private bool TryResolveStoredPath(MealImageKind kind, string fileName, bool thumbnail, out string requestPath)
    {
        requestPath = string.Empty;
        var storePath = _options.StorePath?.Trim();
        if (string.IsNullOrWhiteSpace(storePath))
        {
            return false;
        }

        var folder = MealImagePaths.GetFolder(kind);
        var newRelative = thumbnail
            ? Path.Combine(folder, MealImagePaths.ThumbSubFolder, fileName)
            : Path.Combine(folder, fileName);

        if (File.Exists(Path.Combine(storePath, newRelative)))
        {
            requestPath = thumbnail
                ? $"/{folder}/{MealImagePaths.ThumbSubFolder}/{fileName}"
                : $"/{folder}/{fileName}";
            return true;
        }

        return false;
    }

    private string? CombineBaseUrl(string path)
    {
        var baseUrl = _options.PublicBaseUrl?.Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return path;
        }

        return $"{baseUrl}{path}";
    }

    public string? BuildSchoolLogoUrl(string? fileName)
    {
        if (TryGetPassthroughImagePath(fileName, out var passthrough))
        {
            if (passthrough.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                || passthrough.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || passthrough.StartsWith("//", StringComparison.Ordinal))
            {
                return passthrough;
            }

            return CombineBaseUrl(passthrough);
        }

        var normalized = NormalizeFileName(fileName);
        if (normalized is null)
        {
            return null;
        }

        return CombineBaseUrl($"/SchoolLogo/{normalized}");
    }
}
