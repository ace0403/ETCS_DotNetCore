using Microsoft.AspNetCore.Http;

namespace ETCS.Shared.Media;

public interface IMealImageStorageService
{
    Task<string?> SaveAsync(IFormFile file, MealImageKind kind, CancellationToken cancellationToken = default);

    Task DeleteAsync(MealImageKind kind, string? fileName, CancellationToken cancellationToken = default);
}
