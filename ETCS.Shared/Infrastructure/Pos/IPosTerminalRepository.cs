namespace ETCS.Shared.Infrastructure.Pos;

public interface IPosTerminalRepository
{
    Task<IReadOnlyList<PosSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<PosTerminalDto>> GetTerminalsAsync(int? schoolId, CancellationToken cancellationToken);

    Task<PosTerminalDto?> GetTerminalByCodeAsync(string terminalCode, CancellationToken cancellationToken);
}
