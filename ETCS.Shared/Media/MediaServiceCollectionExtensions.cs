using ETCS.Shared.Options;
using Microsoft.Extensions.DependencyInjection;

namespace ETCS.Shared.Media;

public static class MediaServiceCollectionExtensions
{
    public static IServiceCollection AddMealImageServices(this IServiceCollection services)
    {
        services.AddSingleton<MealImageUrlBuilder>();
        services.AddSingleton<IMealImageStorageService, MealImageStorageService>();
        return services;
    }

    public static IServiceCollection ConfigureMediaOptions(
        this IServiceCollection services,
        Microsoft.Extensions.Configuration.IConfiguration configuration,
        string? storePathSection = null)
    {
        services.Configure<MediaOptions>(configuration.GetSection(MediaOptions.SectionName));

        if (!string.IsNullOrWhiteSpace(storePathSection))
        {
            services.PostConfigure<MediaOptions>(options =>
            {
                if (string.IsNullOrWhiteSpace(options.StorePath))
                {
                    options.StorePath = configuration.GetSection(storePathSection)["StorePath"] ?? string.Empty;
                }
            });
        }

        services.AddMealImageServices();
        return services;
    }
}
