namespace ETCS.Shared.Infrastructure.Data;

public interface IDbHealthRepository
{
    Task<int> PingAsync(CancellationToken cancellationToken);
}
