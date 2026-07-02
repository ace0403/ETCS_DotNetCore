using ETCS.Shared.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace ETCS.Shared.Media;

public sealed class MealImageStorageService : IMealImageStorageService
{
    private const int ThumbnailMaxDimension = 200;

    private readonly MediaOptions _options;
    private readonly ILogger<MealImageStorageService> _logger;

    public MealImageStorageService(IOptions<MediaOptions> options, ILogger<MealImageStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> SaveAsync(IFormFile file, MealImageKind kind, CancellationToken cancellationToken = default)
    {
        var storePath = _options.StorePath?.Trim();
        if (string.IsNullOrWhiteSpace(storePath) || file.Length == 0)
        {
            return null;
        }

        var folder = MealImagePaths.GetFolder(kind);
        var fullDir = Path.Combine(storePath, folder);
        var thumbDir = Path.Combine(fullDir, MealImagePaths.ThumbSubFolder);
        Directory.CreateDirectory(fullDir);
        Directory.CreateDirectory(thumbDir);

        var extension = NormalizeExtension(Path.GetExtension(file.FileName));
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var fullPath = Path.Combine(fullDir, fileName);
        var thumbPath = Path.Combine(thumbDir, fileName);

        await using (var uploadStream = file.OpenReadStream())
        await using (var outputStream = File.Create(fullPath))
        {
            await uploadStream.CopyToAsync(outputStream, cancellationToken);
        }

        try
        {
            await using var imageStream = File.OpenRead(fullPath);
            using var image = await Image.LoadAsync(imageStream, cancellationToken);
            image.Mutate(ctx => ctx.Resize(new ResizeOptions
            {
                Mode = ResizeMode.Max,
                Size = new Size(ThumbnailMaxDimension, ThumbnailMaxDimension)
            }));

            await using var thumbStream = File.Create(thumbPath);
            var encoder = extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? (IImageEncoder)new PngEncoder()
                : new JpegEncoder { Quality = 85 };
            await image.SaveAsync(thumbStream, encoder, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail generation failed for {FileName}; full image was saved.", fileName);
            await using var fallbackStream = File.OpenRead(fullPath);
            await using var thumbStream = File.Create(thumbPath);
            await fallbackStream.CopyToAsync(thumbStream, cancellationToken);
        }

        return fileName;
    }

    public Task DeleteAsync(MealImageKind kind, string? fileName, CancellationToken cancellationToken = default)
    {
        var normalized = MealImageUrlBuilder.NormalizeFileName(fileName);
        if (normalized is null)
        {
            return Task.CompletedTask;
        }

        var storePath = _options.StorePath?.Trim();
        if (string.IsNullOrWhiteSpace(storePath))
        {
            return Task.CompletedTask;
        }

        var folder = MealImagePaths.GetFolder(kind);
        DeleteIfExists(Path.Combine(storePath, folder, normalized));
        DeleteIfExists(Path.Combine(storePath, folder, MealImagePaths.ThumbSubFolder, normalized));

        return Task.CompletedTask;
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return ".jpg";
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => normalized == ".jpeg" ? ".jpg" : normalized,
            _ => ".jpg"
        };
    }
}
