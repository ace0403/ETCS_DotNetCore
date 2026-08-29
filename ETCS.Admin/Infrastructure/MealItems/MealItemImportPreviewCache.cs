using ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;
using Microsoft.Extensions.Caching.Memory;

namespace ETCS.Admin.Infrastructure.MealItems;

public interface IMealItemImportPreviewCache
{
    string Store(MealItemImportCacheEntry entry);
    MealItemImportCacheEntry? Get(string importToken);
    void Remove(string importToken);
}

public sealed class MealItemImportPreviewCache : IMealItemImportPreviewCache
{
    private const string CacheKeyPrefix = "meal-item-import:";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    private readonly IMemoryCache _cache;

    public MealItemImportPreviewCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    public string Store(MealItemImportCacheEntry entry)
    {
        var token = Guid.NewGuid().ToString("N");
        _cache.Set(BuildKey(token), entry, CacheDuration);
        return token;
    }

    public MealItemImportCacheEntry? Get(string importToken)
    {
        if (string.IsNullOrWhiteSpace(importToken))
        {
            return null;
        }

        return _cache.TryGetValue(BuildKey(importToken), out MealItemImportCacheEntry? entry)
            ? entry
            : null;
    }

    public void Remove(string importToken)
    {
        if (!string.IsNullOrWhiteSpace(importToken))
        {
            _cache.Remove(BuildKey(importToken));
        }
    }

    private static string BuildKey(string importToken) => $"{CacheKeyPrefix}{importToken}";
}
