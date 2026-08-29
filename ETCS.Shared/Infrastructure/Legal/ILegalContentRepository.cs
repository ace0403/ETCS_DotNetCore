namespace ETCS.Shared.Infrastructure.Legal;

public interface ILegalContentRepository
{
    Task<LegalContentDto?> GetByKeyAsync(string contentKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LegalContentDto>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    void ClearCache();
}
