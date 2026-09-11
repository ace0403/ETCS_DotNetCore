using ETCS.Pos.Web.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Pos.Web.Services;

public interface IReceiptLogoLoader
{
    string? LoadLogoBase64();
}

public sealed class ReceiptLogoLoader : IReceiptLogoLoader
{
    private readonly IWebHostEnvironment _environment;
    private readonly ReceiptBrandingOptions _options;

    public ReceiptLogoLoader(IWebHostEnvironment environment, IOptions<ReceiptBrandingOptions> options)
    {
        _environment = environment;
        _options = options.Value;
    }

    public string? LoadLogoBase64()
    {
        var relativePath = (_options.LogoPath ?? string.Empty).Trim()
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return null;
        }

        if (relativePath.StartsWith("wwwroot" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        {
            relativePath = relativePath["wwwroot".Length..].TrimStart(Path.DirectorySeparatorChar);
        }

        var absolutePath = Path.Combine(_environment.WebRootPath, relativePath);
        if (!File.Exists(absolutePath))
        {
            return null;
        }

        var bytes = File.ReadAllBytes(absolutePath);
        if (bytes.Length == 0)
        {
            return null;
        }

        var extension = Path.GetExtension(absolutePath).ToLowerInvariant();
        var mimeType = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".bmp" => "image/bmp",
            _ => "image/png"
        };

        return "data:" + mimeType + ";base64," + Convert.ToBase64String(bytes);
    }
}
