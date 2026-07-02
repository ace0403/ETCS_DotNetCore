using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.FileProviders;

namespace ETCS.Shared.Media;

public static class MealImageStaticFileExtensions
{
    public static void MapMealImageStaticFiles(this WebApplication app, string? storePath)
    {
        storePath = storePath?.Trim();
        if (string.IsNullOrWhiteSpace(storePath))
        {
            return;
        }

        MapFolder(app, storePath, MealImagePaths.MealItemFolder, $"/{MealImagePaths.MealItemFolder}");
        MapFolder(
            app,
            storePath,
            Path.Combine(MealImagePaths.MealItemFolder, MealImagePaths.ThumbSubFolder),
            $"/{MealImagePaths.MealItemFolder}/{MealImagePaths.ThumbSubFolder}");
        MapFolder(app, storePath, MealImagePaths.MealComboFolder, $"/{MealImagePaths.MealComboFolder}");
        MapFolder(
            app,
            storePath,
            Path.Combine(MealImagePaths.MealComboFolder, MealImagePaths.ThumbSubFolder),
            $"/{MealImagePaths.MealComboFolder}/{MealImagePaths.ThumbSubFolder}");

        MapFolder(app, storePath, "images", "/images");
    }

    private static void MapFolder(WebApplication app, string storePath, string relativeFolder, string requestPath)
    {
        var physicalPath = Path.Combine(storePath, relativeFolder);
        Directory.CreateDirectory(physicalPath);

        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(physicalPath),
            RequestPath = requestPath
        });
    }
}
