using System.Data;
using Dapper;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosCatalogRepository : IPosCatalogRepository
{
    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly IPosLegacyTransactionRepository _legacyRepository;

    public PosCatalogRepository(
        IMealDbConnectionFactory connectionFactory,
        IPosLegacyTransactionRepository legacyRepository)
    {
        _connectionFactory = connectionFactory;
        _legacyRepository = legacyRepository;
    }

    public async Task<IReadOnlyList<PosCategoryDto>> GetCategoriesAsync(int schoolId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT DISTINCT
                mi.MealCategotyId AS CategoryId,
                LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) AS CategoryName,
                mc.SortOrder
            FROM MealItem mi
            LEFT JOIN Enums mc ON mi.MealCategotyId = mc.Id
            WHERE ISNULL(mi.IsDeleted, 0) = 0
              AND ISNULL(mi.IsActive, 1) = 1
              AND mi.MealCategotyId IS NOT NULL
              AND (
                  EXISTS (
                      SELECT 1
                      FROM MealItemSchools mis
                      WHERE mis.MealItemId = mi.Id
                        AND mis.SchoolId = @SchoolId
                  )
                  OR (
                      NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                      AND mi.SchoolId = @SchoolId
                  )
              )
              AND EXISTS (
                  SELECT 1
                  FROM MealItemOrderTypes miot
                  WHERE miot.MealItemId = mi.Id
                    AND miot.OrderTypeId = @PosOrderTypeId
              )
            ORDER BY mc.SortOrder;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<PosCategoryDto>(new CommandDefinition(
            sql,
            new
            {
                SchoolId = schoolId,
                PosOrderTypeId = (int)TransactionTypeEnum.POS
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<PosCatalogItemDto>> GetItemsAsync(
        int schoolId,
        int? categoryId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                mi.Id,
                LTRIM(RTRIM(ISNULL(mi.ItemName, ''))) AS ItemName,
                mi.Price,
                LTRIM(RTRIM(ISNULL(mi.ImageName, ''))) AS ImageName,
                mi.MealCategotyId AS MealCategoryId,
                LTRIM(RTRIM(ISNULL(mc.EnumValue, ''))) AS CategoryName
            FROM MealItem mi
            LEFT JOIN Enums mc ON mi.MealCategotyId = mc.Id
            WHERE ISNULL(mi.IsDeleted, 0) = 0
              AND ISNULL(mi.IsActive, 1) = 1
              AND (@CategoryId IS NULL OR mi.MealCategotyId = @CategoryId)
              AND (
                  EXISTS (
                      SELECT 1
                      FROM MealItemSchools mis
                      WHERE mis.MealItemId = mi.Id
                        AND mis.SchoolId = @SchoolId
                  )
                  OR (
                      NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                      AND mi.SchoolId = @SchoolId
                  )
              )
              AND EXISTS (
                  SELECT 1
                  FROM MealItemOrderTypes miot
                  WHERE miot.MealItemId = mi.Id
                    AND miot.OrderTypeId = @PosOrderTypeId
              )
            ORDER BY mi.ItemName;
            """;

        using var connection = _connectionFactory.CreateConnection();
        var rows = (await connection.QueryAsync<PosCatalogItemDto>(new CommandDefinition(
            sql,
            new
            {
                SchoolId = schoolId,
                CategoryId = categoryId,
                PosOrderTypeId = (int)TransactionTypeEnum.POS
            },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken))).ToList();

        if (rows.Count == 0)
        {
            return rows;
        }

        var codeMap = await _legacyRepository.GetItemCodesByMealItemIdsAsync(
            rows.Select(r => r.Id).ToList(),
            cancellationToken);

        return rows.Select(row =>
        {
            var itemCode = codeMap.TryGetValue(row.Id, out var code) && !string.IsNullOrWhiteSpace(code)
                ? code
                : row.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return new PosCatalogItemDto
            {
                Id = row.Id,
                ItemCode = itemCode,
                ItemName = row.ItemName,
                Price = row.Price,
                ImageName = row.ImageName,
                MealCategoryId = row.MealCategoryId,
                CategoryName = row.CategoryName
            };
        }).ToList();
    }
}
