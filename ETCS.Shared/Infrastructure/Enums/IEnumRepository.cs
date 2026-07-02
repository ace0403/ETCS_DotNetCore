namespace ETCS.Shared.Infrastructure.Enums;

public interface IEnumRepository
{
    Task<IReadOnlyList<EnumTypeListItemDto>> GetActiveTypeListAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<EnumDetailDto>> GetByTypeIdsAsync(
        IReadOnlyCollection<int> typeIds,
        CancellationToken cancellationToken);

    Task<EnumDetailDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken);
}
