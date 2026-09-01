using System.Data.Common;
using Dapper;

namespace ETCS.Shared.Infrastructure.Students;

public static class StudentIdMemberCustomerIdSync
{
    private const string UpdateSql = """
        UPDATE IdMember
        SET CustomerID = @NewCustomerId
        WHERE LTRIM(RTRIM(ISNULL(CustomerID, ''))) = LTRIM(RTRIM(@OldCustomerId))
          AND LTRIM(RTRIM(ISNULL(CustomerID, ''))) <> '';
        """;

    public static async Task<int> UpdateCustomerIdAsync(
        DbConnection connection,
        string oldCustomerId,
        string newCustomerId,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        var oldId = oldCustomerId.Trim();
        var newId = newCustomerId.Trim();
        if (oldId.Length == 0 || newId.Length == 0)
        {
            return 0;
        }

        if (string.Equals(oldId, newId, StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        return await connection.ExecuteAsync(
            new CommandDefinition(
                UpdateSql,
                new { OldCustomerId = oldId, NewCustomerId = newId },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }
}
