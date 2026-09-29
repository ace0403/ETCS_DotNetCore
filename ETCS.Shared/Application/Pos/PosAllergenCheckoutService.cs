using System.Data;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Meals.Menu;
using ETCS.Shared.Infrastructure.Pos;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Application.Pos;

public interface IPosAllergenCheckoutService
{
    Task<PosAllergenCheckoutEvaluateResponse?> EvaluateAsync(
        PosAllergenCheckoutEvaluateRequest request,
        CancellationToken cancellationToken);
}

public sealed class PosAllergenCheckoutService : IPosAllergenCheckoutService
{
    private readonly IPosSpendRepository _spendRepository;
    private readonly IPosNfcMemberRepository _nfcMemberRepository;
    private readonly IOrderItemAllergenResolver _allergenResolver;
    private readonly IMealDbConnectionFactory _mealDbConnectionFactory;
    private readonly IDbConnectionFactory _ibonusConnectionFactory;
    private readonly int _orderTypeId;

    public PosAllergenCheckoutService(
        IPosSpendRepository spendRepository,
        IPosNfcMemberRepository nfcMemberRepository,
        IOrderItemAllergenResolver allergenResolver,
        IMealDbConnectionFactory mealDbConnectionFactory,
        IDbConnectionFactory ibonusConnectionFactory,
        IOptions<PosOptions> posOptions)
    {
        _spendRepository = spendRepository;
        _nfcMemberRepository = nfcMemberRepository;
        _allergenResolver = allergenResolver;
        _mealDbConnectionFactory = mealDbConnectionFactory;
        _ibonusConnectionFactory = ibonusConnectionFactory;
        _orderTypeId = posOptions.Value.OrderTypeId;
    }

    public async Task<PosAllergenCheckoutEvaluateResponse?> EvaluateAsync(
        PosAllergenCheckoutEvaluateRequest request,
        CancellationToken cancellationToken)
    {
        var mealItemIds = request.MealItemIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (mealItemIds.Count == 0)
        {
            return new PosAllergenCheckoutEvaluateResponse();
        }

        var customerId = await ResolveCustomerIdAsync(request, cancellationToken);
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        var spend = await _spendRepository.GetSpendInfoByCustomerIdAsync(
            customerId,
            _orderTypeId,
            cancellationToken);
        if (spend?.StudentId is not > 0)
        {
            return null;
        }

        var studentId = spend.StudentId.Value;
        var studentProfile = await GetStudentProfileAsync(studentId, customerId, cancellationToken);
        var registeredAllergens = await GetRegisteredAllergensAsync(studentId, cancellationToken);

        var resolverLines = mealItemIds
            .Select(id => ((int?)id, (int?)null))
            .ToList();

        var allergenMap = await _allergenResolver.ResolveAsync(studentId, resolverLines, cancellationToken);
        var itemNames = await GetMealItemNamesAsync(mealItemIds, cancellationToken);

        var conflicts = new List<PosAllergenCheckoutConflictDto>();
        var summary = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mealItemId in mealItemIds)
        {
            var key = ((int?)mealItemId, (int?)null);
            if (!allergenMap.TryGetValue(key, out var overlap) || overlap.Count == 0)
            {
                continue;
            }

            itemNames.TryGetValue(mealItemId, out var itemName);
            foreach (var name in overlap)
            {
                summary.Add(name);
            }

            conflicts.Add(new PosAllergenCheckoutConflictDto
            {
                MealItemId = mealItemId,
                ItemName = string.IsNullOrWhiteSpace(itemName) ? "Meal item" : itemName.Trim(),
                AllergenNames = overlap
            });
        }

        return new PosAllergenCheckoutEvaluateResponse
        {
            CustomerId = customerId,
            StudentId = studentId,
            StudentName = studentProfile.StudentName,
            RegisteredAllergens = registeredAllergens,
            Conflicts = conflicts,
            HasConflict = conflicts.Count > 0,
            CartAllergenSummary = summary.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private async Task<string?> ResolveCustomerIdAsync(
        PosAllergenCheckoutEvaluateRequest request,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return request.CustomerId.Trim();
        }

        var candidates = PosNfcCardSnNormalizer.BuildCandidates(
            request.CardSn,
            request.UidHex,
            request.UidHexReversed,
            request.UidDecimal,
            request.UidDecimalReversed);

        if (candidates.Count == 0)
        {
            return null;
        }

        var member = await _nfcMemberRepository.FindByCardSnAsync(candidates, cancellationToken);
        return string.IsNullOrWhiteSpace(member?.CustomerId) ? null : member.CustomerId.Trim();
    }

    private async Task<StudentProfileRow> GetStudentProfileAsync(
        int studentId,
        string customerId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT TOP (1)
                LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId,
                LTRIM(RTRIM(
                    CONCAT(
                        LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))),
                        CASE WHEN LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) <> '' THEN ' ' ELSE '' END,
                        LTRIM(RTRIM(ISNULL(sl.StudLastName, '')))
                    )
                )) AS StudentName
            FROM StudentLogin sl
            WHERE sl.UserId = @StudentId;
            """;

        using var connection = _ibonusConnectionFactory.CreateConnection();
        var row = await connection.QueryFirstOrDefaultAsync<StudentProfileRow>(new CommandDefinition(
            sql,
            new { StudentId = studentId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return row ?? new StudentProfileRow
        {
            CustomerId = customerId,
            StudentName = string.Empty
        };
    }

    private async Task<IReadOnlyList<string>> GetRegisteredAllergensAsync(
        int studentId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT LTRIM(RTRIM(e.EnumValue)) AS AllergenName
            FROM StudentAllergies sa
            INNER JOIN Enums e ON e.Id = sa.AllergyItemId
            WHERE sa.StudentId = @StudentId
              AND ISNULL(e.IsActive, 1) = 1
              AND LTRIM(RTRIM(ISNULL(e.EnumValue, ''))) <> ''
            ORDER BY e.EnumValue;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<string>(new CommandDefinition(
            sql,
            new { StudentId = studentId },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<int, string>> GetMealItemNamesAsync(
        IReadOnlyList<int> mealItemIds,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT Id, LTRIM(RTRIM(ISNULL(ItemName, ''))) AS ItemName
            FROM MealItem
            WHERE Id IN @MealItemIds;
            """;

        using var connection = _mealDbConnectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<MealItemNameRow>(new CommandDefinition(
            sql,
            new { MealItemIds = mealItemIds },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows
            .Where(r => r.Id > 0)
            .ToDictionary(r => r.Id, r => r.ItemName ?? string.Empty);
    }

    private sealed class StudentProfileRow
    {
        public string CustomerId { get; init; } = string.Empty;
        public string StudentName { get; init; } = string.Empty;
    }

    private sealed class MealItemNameRow
    {
        public int Id { get; init; }
        public string? ItemName { get; init; }
    }
}
