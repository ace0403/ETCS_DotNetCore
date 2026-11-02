using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Audit;

/// <summary>
/// Replaces foreign-key ids in an audit payload with the related display values.
/// </summary>
internal static class AuditReferenceRewriter
{
    private enum ReferenceKind
    {
        Account,
        School,
        Guardian,
        Grade,
        Country,
        EnumValue,
        Student,
        Role,
        Module
    }

    private static readonly Dictionary<string, ReferenceKind> Properties = new(StringComparer.OrdinalIgnoreCase)
    {
        ["CreatedBy"] = ReferenceKind.Account,
        ["UpdatedBy"] = ReferenceKind.Account,
        ["SchoolId"] = ReferenceKind.School,
        ["SchoolIds"] = ReferenceKind.School,
        ["GuardianId"] = ReferenceKind.Guardian,
        ["GradeId"] = ReferenceKind.Grade,
        ["CountryId"] = ReferenceKind.Country,
        ["MealSessionId"] = ReferenceKind.EnumValue,
        ["MealTypeId"] = ReferenceKind.EnumValue,
        ["MealCategoryId"] = ReferenceKind.EnumValue,
        ["IngredientId"] = ReferenceKind.EnumValue,
        ["IngredientIds"] = ReferenceKind.EnumValue,
        ["DayId"] = ReferenceKind.EnumValue,
        ["DayIds"] = ReferenceKind.EnumValue,
        ["OrderTypeId"] = ReferenceKind.EnumValue,
        ["OrderTypeIds"] = ReferenceKind.EnumValue,
        ["AllergyItemId"] = ReferenceKind.EnumValue,
        ["AllergyItemIds"] = ReferenceKind.EnumValue,
        ["NutritionId"] = ReferenceKind.EnumValue,
        ["MeasureTypeId"] = ReferenceKind.EnumValue,
        ["ParentId"] = ReferenceKind.EnumValue,
        ["FromUserId"] = ReferenceKind.Student,
        ["ToUserId"] = ReferenceKind.Student,
        ["StudentId"] = ReferenceKind.Student,
        ["RoleId"] = ReferenceKind.Role,
        ["RoleIds"] = ReferenceKind.Role,
        ["DefaultRoleId"] = ReferenceKind.Role,
        ["ActiveRoleId"] = ReferenceKind.Role,
        ["ModuleId"] = ReferenceKind.Module
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null
    };

    public static async Task<JsonNode?> RewriteAsync(
        object? value,
        IDbConnectionFactory legacyConnectionFactory,
        IMealDbConnectionFactory mealConnectionFactory,
        CancellationToken cancellationToken)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var node = value is JsonNode existing
                ? existing
                : JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
            if (node is null)
            {
                return null;
            }

            var ids = new Dictionary<ReferenceKind, HashSet<int>>();
            Collect(node, ids);
            if (ids.Count == 0)
            {
                return node;
            }

            var names = await LoadNamesAsync(legacyConnectionFactory, mealConnectionFactory, ids, cancellationToken);
            Replace(node, names);
            return node;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return value is JsonNode existing
                ? existing
                : JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
        }
    }

    private static void Collect(JsonNode? node, Dictionary<ReferenceKind, HashSet<int>> ids)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (ShouldResolve(obj, property.Key, out var kind))
                    {
                        CollectIds(property.Value, kind, ids);
                    }

                    Collect(property.Value, ids);
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Collect(item, ids);
                }

                break;
        }
    }

    private static void CollectIds(JsonNode? node, ReferenceKind kind, Dictionary<ReferenceKind, HashSet<int>> ids)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                AddId(item, kind, ids);
            }

            return;
        }

        AddId(node, kind, ids);
    }

    private static void AddId(JsonNode? node, ReferenceKind kind, Dictionary<ReferenceKind, HashSet<int>> ids)
    {
        if (!TryReadId(node, out var id))
        {
            return;
        }

        if (!ids.TryGetValue(kind, out var set))
        {
            set = [];
            ids[kind] = set;
        }

        set.Add(id);
    }

    private static void Replace(JsonNode? node, IReadOnlyDictionary<ReferenceKind, Dictionary<int, string>> names)
    {
        if (node is not JsonObject obj)
        {
            if (node is JsonArray array)
            {
                foreach (var item in array)
                {
                    Replace(item, names);
                }
            }

            return;
        }

        foreach (var property in obj.ToList())
        {
            if (ShouldResolve(obj, property.Key, out var kind) && names.TryGetValue(kind, out var map))
            {
                obj[property.Key] = ReplaceValue(property.Value, map);
            }

            Replace(obj[property.Key], names);
        }
    }

    private static JsonNode? ReplaceValue(JsonNode? node, IReadOnlyDictionary<int, string> names)
    {
        if (node is JsonArray array)
        {
            var replaced = new JsonArray();
            foreach (var item in array)
            {
                replaced.Add(ReplaceScalar(item, names));
            }

            return replaced;
        }

        return ReplaceScalar(node, names);
    }

    private static JsonNode? ReplaceScalar(JsonNode? node, IReadOnlyDictionary<int, string> names)
    {
        if (!TryReadId(node, out var id))
        {
            return node?.DeepClone();
        }

        return names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name)
            ? JsonValue.Create(name.Trim())
            : node?.DeepClone();
    }

    private static bool ShouldResolve(JsonObject obj, string propertyName, out ReferenceKind kind)
    {
        if (!Properties.TryGetValue(propertyName, out kind))
        {
            return false;
        }

        // The role record's own id. Its name is already stored beside it.
        if (kind == ReferenceKind.Role
            && propertyName.Equals("RoleId", StringComparison.OrdinalIgnoreCase)
            && obj.ContainsKey("RoleName"))
        {
            return false;
        }

        return true;
    }

    private static bool TryReadId(JsonNode? node, out int id)
    {
        id = 0;
        if (node is not JsonValue value)
        {
            return false;
        }

        if (value.TryGetValue<int>(out var intId) && intId > 0)
        {
            id = intId;
            return true;
        }

        if (value.TryGetValue<long>(out var longId) && longId > 0 && longId <= int.MaxValue)
        {
            id = (int)longId;
            return true;
        }

        if (value.TryGetValue<decimal>(out var decimalId)
            && decimalId > 0
            && decimalId <= int.MaxValue
            && decimalId == decimal.Truncate(decimalId))
        {
            id = (int)decimalId;
            return true;
        }

        return false;
    }

    private static async Task<Dictionary<ReferenceKind, Dictionary<int, string>>> LoadNamesAsync(
        IDbConnectionFactory legacyConnectionFactory,
        IMealDbConnectionFactory mealConnectionFactory,
        Dictionary<ReferenceKind, HashSet<int>> ids,
        CancellationToken cancellationToken)
    {
        var names = new Dictionary<ReferenceKind, Dictionary<int, string>>();
        foreach (var (kind, idSet) in ids)
        {
            names[kind] = kind switch
            {
                ReferenceKind.Account => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT CAST(Sid AS int) AS Id,
                           CASE
                               WHEN LTRIM(RTRIM(ISNULL(FirstName, '') + ISNULL(LastName, ''))) = ''
                                   THEN LTRIM(RTRIM(ISNULL(LoginName, '')))
                               ELSE LTRIM(RTRIM(ISNULL(FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(LastName, '')))
                           END AS Name
                    FROM LoginAccount
                    WHERE Sid IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.School => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT SchoolId AS Id, LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
                    FROM SchoolInfo
                    WHERE SchoolId IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Guardian => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT CAST(GrdID AS int) AS Id,
                           LTRIM(RTRIM(ISNULL(FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(LastName, ''))) AS Name
                    FROM GuardianInfo
                    WHERE GrdID IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Grade => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT Id, LTRIM(RTRIM(ISNULL(Grade, ''))) AS Name
                    FROM SchoolGrades
                    WHERE Id IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Country => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT CountryId AS Id, LTRIM(RTRIM(ISNULL(CountryName, ''))) AS Name
                    FROM CountryInfo
                    WHERE CountryId IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Student => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT CAST(UserId AS int) AS Id,
                           LTRIM(RTRIM(ISNULL(StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(StudLastName, ''))) AS Name
                    FROM StudentLogin
                    WHERE UserId IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Role => await LoadMapAsync(legacyConnectionFactory.CreateConnection, """
                    SELECT RoleID AS Id, LTRIM(RTRIM(ISNULL(RoleName, ''))) AS Name
                    FROM RoleInfo
                    WHERE RoleID IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.EnumValue => await LoadMapAsync(mealConnectionFactory.CreateConnection, """
                    SELECT Id, LTRIM(RTRIM(ISNULL(EnumValue, ''))) AS Name
                    FROM Enums
                    WHERE Id IN @Ids;
                    """, idSet, cancellationToken),
                ReferenceKind.Module => await LoadMapAsync(mealConnectionFactory.CreateConnection, """
                    SELECT ModuleId AS Id, LTRIM(RTRIM(ISNULL(DisplayName, ''))) AS Name
                    FROM AdminModule
                    WHERE ModuleId IN @Ids;
                    """, idSet, cancellationToken),
                _ => []
            };
        }

        return names;
    }

    private static async Task<Dictionary<int, string>> LoadMapAsync(
        Func<System.Data.IDbConnection> createConnection,
        string sql,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return [];
        }

        try
        {
            using var connection = createConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            var rows = await dbConnection.QueryAsync<IdNameRow>(
                new CommandDefinition(sql, new { Ids = distinct }, cancellationToken: cancellationToken));
            return rows
                .Where(row => !string.IsNullOrWhiteSpace(row.Name))
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.First().Name.Trim());
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private sealed class IdNameRow
    {
        public int Id { get; set; }

        public string Name { get; init; } = string.Empty;
    }
}
