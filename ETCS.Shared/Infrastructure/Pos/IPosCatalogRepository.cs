namespace ETCS.Shared.Infrastructure.Pos;

public interface IPosCatalogRepository
{
    Task<IReadOnlyList<PosCategoryDto>> GetCategoriesAsync(int schoolId, CancellationToken cancellationToken);

    Task<IReadOnlyList<PosCatalogItemDto>> GetItemsAsync(
        int schoolId,
        int? categoryId,
        CancellationToken cancellationToken);
}
