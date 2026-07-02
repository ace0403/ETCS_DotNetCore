using ETCS.Shared.Infrastructure.Meals;
using Microsoft.Extensions.Caching.Memory;

namespace ETCS.API.Infrastructure.Caching;

public sealed class CachedMealRepository : IMealRepository
{
    private static readonly TimeSpan MealMenuCacheTtl = TimeSpan.FromMinutes(5);

    private readonly MealRepository _inner;
    private readonly IMemoryCache _cache;

    public CachedMealRepository(MealRepository inner, IMemoryCache cache)
    {
        _inner = inner;
        _cache = cache;
    }

    public async Task<IReadOnlyList<MealItemDto>> GetMealItemsForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildItemsCacheKey(studentId, schoolId, mealDate, mealTypeId);
        if (_cache.TryGetValue(key, out IReadOnlyList<MealItemDto>? cached) && cached is not null)
        {
            return cached;
        }

        var items = await _inner.GetMealItemsForStudentAsync(studentId, schoolId, mealDate, mealTypeId, cancellationToken);
        _cache.Set(key, items, MealMenuCacheTtl);
        return items;
    }

    public async Task<IReadOnlyList<MealPackageDto>> GetMealPackagesForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default)
    {
        var key = BuildPackagesCacheKey(studentId, schoolId, mealDate, mealTypeId);
        if (_cache.TryGetValue(key, out IReadOnlyList<MealPackageDto>? cached) && cached is not null)
        {
            return cached;
        }

        var packages = await _inner.GetMealPackagesForStudentAsync(studentId, schoolId, mealDate, mealTypeId, cancellationToken);
        _cache.Set(key, packages, MealMenuCacheTtl);
        return packages;
    }

    public static string BuildItemsCacheKey(int studentId, int schoolId, DateTime mealDate, int? mealTypeId) =>
        $"meal-items:{studentId}:{schoolId}:{mealDate:yyyy-MM-dd}:{mealTypeId?.ToString() ?? "all"}";

    public static string BuildPackagesCacheKey(int studentId, int schoolId, DateTime mealDate, int? mealTypeId) =>
        $"meal-packages:{studentId}:{schoolId}:{mealDate:yyyy-MM-dd}:{mealTypeId?.ToString() ?? "all"}:{DateTime.Now:yyyyMMddHH}";
}
