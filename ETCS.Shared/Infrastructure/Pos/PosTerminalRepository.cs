using System.Data;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosTerminalRepository : IPosTerminalRepository
{
    private readonly IDbConnectionFactory _connectionFactory;

    public PosTerminalRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<PosSchoolDto>> GetSchoolsAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                s.SchoolId,
                LTRIM(RTRIM(ISNULL(s.SchoolName, ''))) AS SchoolName,
                LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) AS SchoolCode
            FROM SchoolInfo s
            ORDER BY s.SchoolName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PosSchoolDto>(new CommandDefinition(
            sql,
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PosTerminalDto>> GetTerminalsAsync(int? schoolId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                LTRIM(RTRIM(ISNULL(t.TerminalCode, ''))) AS TerminalCode,
                LTRIM(RTRIM(ISNULL(t.Description, ''))) AS TerminalName,
                LTRIM(RTRIM(ISNULL(CAST(t.BranchCode AS varchar(20)), ''))) AS BranchCode,
                LTRIM(RTRIM(ISNULL(CAST(t.IPaddress AS varchar(50)), ''))) AS IpAddress,
                s.SchoolId,
                1 AS IsActive
            FROM Terminals t
            LEFT JOIN SchoolInfo s
                ON LTRIM(RTRIM(CAST(s.Schoolcode AS varchar(20)))) = LTRIM(RTRIM(CAST(t.BranchCode AS varchar(20))))
            WHERE @SchoolId IS NULL
               OR s.SchoolId = @SchoolId
               OR LTRIM(RTRIM(CAST(t.BranchCode AS varchar(20)))) = (
                    SELECT TOP (1) LTRIM(RTRIM(CAST(si.Schoolcode AS varchar(20))))
                    FROM SchoolInfo si
                    WHERE si.SchoolId = @SchoolId)
            ORDER BY t.Description;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PosTerminalDto>(new CommandDefinition(
            sql,
            new { SchoolId = schoolId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return rows.Select(NormalizeTerminal).ToList();
    }

    public async Task<PosTerminalDto?> GetTerminalByCodeAsync(string terminalCode, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                LTRIM(RTRIM(ISNULL(t.TerminalCode, ''))) AS TerminalCode,
                LTRIM(RTRIM(ISNULL(t.Description, ''))) AS TerminalName,
                LTRIM(RTRIM(ISNULL(CAST(t.BranchCode AS varchar(20)), ''))) AS BranchCode,
                LTRIM(RTRIM(ISNULL(CAST(t.IPaddress AS varchar(50)), ''))) AS IpAddress,
                s.SchoolId,
                1 AS IsActive
            FROM Terminals t
            LEFT JOIN SchoolInfo s
                ON LTRIM(RTRIM(CAST(s.Schoolcode AS varchar(20)))) = LTRIM(RTRIM(CAST(t.BranchCode AS varchar(20))))
            WHERE LTRIM(RTRIM(t.TerminalCode)) = @TerminalCode;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<PosTerminalDto>(new CommandDefinition(
            sql,
            new { TerminalCode = terminalCode.Trim() },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return row is null ? null : NormalizeTerminal(row);
    }

    private static PosTerminalDto NormalizeTerminal(PosTerminalDto row)
    {
        var digits = new string(row.TerminalCode.Where(char.IsDigit).ToArray());
        return new PosTerminalDto
        {
            TerminalCode = row.TerminalCode,
            TerminalName = row.TerminalName,
            BranchCode = row.BranchCode,
            TerminalCodeNumeric = digits,
            IpAddress = row.IpAddress,
            SchoolId = row.SchoolId,
            IsActive = row.IsActive
        };
    }
}
