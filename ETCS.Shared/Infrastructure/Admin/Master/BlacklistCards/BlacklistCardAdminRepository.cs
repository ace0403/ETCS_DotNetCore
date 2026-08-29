using System.Data;
using System.Data.Common;
using System.Globalization;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Infrastructure.Admin.Master.BlacklistCards;

public sealed class BlacklistCardAdminRepository : IBlacklistCardAdminRepository
{
    private const int CardIdMaxLength = 20;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<BlacklistCardAdminRepository> _logger;

    public BlacklistCardAdminRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<BlacklistCardAdminRepository> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<int?> GetStudentSchoolIdAsync(string customerId, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCardId(customerId);
        if (normalized is null)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var schoolId = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                """
                SELECT TOP (1) CAST(ISNULL(sl.StudSchoolId, 0) AS int)
                FROM StudentLogin sl
                WHERE LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = @CustomerId
                   OR LTRIM(RTRIM(ISNULL(sl.StudCode, ''))) = @CustomerId;
                """,
                new { CustomerId = normalized },
                cancellationToken: cancellationToken));

        return schoolId is > 0 ? schoolId : null;
    }

    public async Task<BlacklistCardLookupResult> GetLinkedCardsAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeCardId(customerId);
        if (normalized is null)
        {
            return BlacklistCardLookupResult.Fail("Student card number required.");
        }

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);

            var items = await GetLinkedCardsInternalAsync(dbConnection, normalized, cancellationToken);
            if (items.Count == 0)
            {
                return BlacklistCardLookupResult.Fail("No data available.");
            }

            return BlacklistCardLookupResult.Ok(items);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load blacklist card info for {CustomerId}.", normalized);
            return BlacklistCardLookupResult.Fail("Unable to load card information. Please try again.");
        }
    }

    public async Task<AdminOperationResult> BlacklistAsync(
        BlacklistCardRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = NormalizeCardId(request.CustomerId);
        if (customerId is null)
        {
            return AdminOperationResult.Fail("Student card number required.");
        }

        var performedBy = NormalizeAuditUser(request.PerformedBy);
        if (performedBy is null)
        {
            return AdminOperationResult.Fail("Your session has expired. Please sign in again.");
        }

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);

            if (!await HasActiveCardAsync(dbConnection, customerId, cancellationToken))
            {
                return AdminOperationResult.Fail("Active cards are not available to block.");
            }

            var parameters = new DynamicParameters();
            parameters.Add("@CustomerID", customerId, DbType.String, size: CardIdMaxLength);
            parameters.Add("@BlockedDate", DateTime.Now, DbType.DateTime);
            parameters.Add("@BlockedBy", performedBy, DbType.String, size: 50);

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "spBlockCustomerId",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

            return AdminOperationResult.Ok("Card blocked.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to blacklist card {CustomerId}.", customerId);
            return AdminOperationResult.Fail("Unable to blacklist the card. Please try again.");
        }
    }

    public async Task<AdminOperationResult> TransferBalanceAsync(
        BlacklistCardTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        var customerId = NormalizeCardId(request.CustomerId);
        var cardSn = NormalizeCardId(request.CardSn);
        if (customerId is null || cardSn is null)
        {
            return AdminOperationResult.Fail("Student card number required.");
        }

        var performedBy = NormalizeAuditUser(request.PerformedBy);
        if (performedBy is null)
        {
            return AdminOperationResult.Fail("Your session has expired. Please sign in again.");
        }

        try
        {
            using var connection = _connectionFactory.CreateConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);

            var cards = await GetLinkedCardsInternalAsync(dbConnection, customerId, cancellationToken);
            var source = cards.FirstOrDefault(c =>
                string.Equals(c.CardSn, cardSn, StringComparison.OrdinalIgnoreCase));

            if (source is null)
            {
                return AdminOperationResult.Fail("Card was not found for this student.");
            }

            if (string.Equals(source.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                return AdminOperationResult.Fail("Transfer is available only for blocked cards.");
            }

            if (string.Equals(source.BalanceTransfer, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                return AdminOperationResult.Fail("Balance has already been transferred.");
            }

            if (!await CheckCardActiveStatusAsync(dbConnection, cardSn, cancellationToken))
            {
                return AdminOperationResult.Fail("This card is not eligible for balance transfer.");
            }

            if (!await HasActiveCardAsync(dbConnection, customerId, cancellationToken))
            {
                return AdminOperationResult.Fail("Active cards are not available to transfer the amount.");
            }

            var balance = await GetPrepaidBalanceAsync(dbConnection, cardSn, cancellationToken);
            if (balance is null)
            {
                return AdminOperationResult.Fail("Unable to read the card balance.");
            }

            var parameters = new DynamicParameters();
            parameters.Add("@CustomerID", customerId, DbType.String, size: CardIdMaxLength);
            parameters.Add("@CardSn", cardSn, DbType.String, size: CardIdMaxLength);
            parameters.Add("@Balance", balance.Value, DbType.Decimal);
            parameters.Add("@TransferDate", DateTime.Now, DbType.DateTime);
            parameters.Add("@TransferBy", performedBy, DbType.String, size: 50);

            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "spCardBalanceTransfer",
                    parameters,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));

            return AdminOperationResult.Ok("Balance transferred to the new active card.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to transfer balance from card {CardSn} for {CustomerId}.", cardSn, customerId);
            return AdminOperationResult.Fail("Unable to transfer the balance. Please try again.");
        }
    }

    private static async Task<IReadOnlyList<BlacklistCardListItemDto>> GetLinkedCardsInternalAsync(
        DbConnection dbConnection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CustomerID", customerId, DbType.String, size: CardIdMaxLength);

        var rows = await dbConnection.QueryAsync<CardBlockStatusRow>(
            new CommandDefinition(
                "spGetCardBlockStatusInfo",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return rows
            .Select(MapListItem)
            .ToList();
    }

    private static async Task<bool> HasActiveCardAsync(
        DbConnection dbConnection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@CustomerID", customerId, DbType.String, size: CardIdMaxLength);

        var result = await dbConnection.ExecuteScalarAsync<object>(
            new CommandDefinition(
                "spCheckActiveCards",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return HasScalarValue(result);
    }

    private static async Task<bool> CheckCardActiveStatusAsync(
        DbConnection dbConnection,
        string cardSn,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@cardsn", cardSn, DbType.String, size: CardIdMaxLength);

        var result = await dbConnection.ExecuteScalarAsync<object>(
            new CommandDefinition(
                "spCheckCardActiveStatus",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return HasScalarValue(result);
    }

    private static async Task<decimal?> GetPrepaidBalanceAsync(
        DbConnection dbConnection,
        string cardSn,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("@Cardsn", cardSn, DbType.String, size: CardIdMaxLength);

        var row = await dbConnection.QueryFirstOrDefaultAsync<BalanceRow>(
            new CommandDefinition(
                "spGetBalanceByCustomerID",
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return row?.BalPrepaid;
    }

    private static BlacklistCardListItemDto MapListItem(CardBlockStatusRow row)
    {
        var status = (row.Status ?? string.Empty).Trim();
        var balanceTransfer = (row.BalanceTransfer ?? string.Empty).Trim();

        return new BlacklistCardListItemDto
        {
            CardSn = (row.CardSN ?? string.Empty).Trim(),
            CustomerId = (row.CustomerID ?? string.Empty).Trim(),
            LastUsed = FormatLastUsed(row.LastVisit),
            Balance = row.BalPrepaid,
            Status = status,
            BalanceTransfer = balanceTransfer,
            CanTransfer = CanTransfer(status, balanceTransfer)
        };
    }

    private static bool CanTransfer(string status, string balanceTransfer) =>
        !string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
        && !string.Equals(balanceTransfer, "Yes", StringComparison.OrdinalIgnoreCase);

    private static string FormatLastUsed(object? value)
    {
        if (value is null or DBNull)
        {
            return string.Empty;
        }

        if (value is DateTime dateTime)
        {
            return dateTime.ToString("dd MMM yyyy HH:mm", CultureInfo.InvariantCulture);
        }

        return Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
    }

    private static bool HasScalarValue(object? value) =>
        value is not null && value is not DBNull;

    private static string? NormalizeCardId(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > CardIdMaxLength ? trimmed[..CardIdMaxLength] : trimmed;
    }

    private static string? NormalizeAuditUser(string? value)
    {
        var trimmed = value?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return null;
        }

        return trimmed.Length > 50 ? trimmed[..50] : trimmed;
    }

    private sealed class CardBlockStatusRow
    {
        public string? CardSN { get; init; }
        public string? CustomerID { get; init; }
        public object? LastVisit { get; init; }
        public decimal BalPrepaid { get; init; }
        public string? Status { get; init; }
        public string? BalanceTransfer { get; init; }
    }

    private sealed class BalanceRow
    {
        public decimal BalPrepaid { get; init; }
    }
}
