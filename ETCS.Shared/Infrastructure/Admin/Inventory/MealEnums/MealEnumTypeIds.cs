using System.Globalization;
using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

public static class MealEnumTypeIds
{
    public const int OrderType = 4;

    public const int FoodAllergy = 5;

    public const int StudentTransactionType = 7;

    public const int WeekDays = 8;

    public const int MealType = 9;

    public const int Duration = 11;

    public const int Nutrition = 12;

    public const int MeasureType = 13;
}

/// <summary>Order type ids exposed to client-side scripts (from <see cref="TransactionTypeEnum"/>).</summary>
public static class OrderTypeJavaScriptIds
{
    public static int MealOrder => (int)TransactionTypeEnum.MealOrder;

    public static int AlaCarte => (int)TransactionTypeEnum.A_La_Carte;

    public static int Pos => (int)TransactionTypeEnum.POS;

    public static IReadOnlyDictionary<string, int> Map { get; } =
        new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["MEAL_ORDER"] = MealOrder,
            ["ALA_CARTE"] = AlaCarte,
            ["POS"] = Pos
        };

    public static IReadOnlyDictionary<string, string> StringMap { get; } =
        Map.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.ToString(CultureInfo.InvariantCulture),
            StringComparer.Ordinal);
}

public static class StudentOrderTypeOptionIds
{
    public static readonly IReadOnlyList<int> Ordered =
    [
        (int)TransactionTypeEnum.Topup,
        (int)TransactionTypeEnum.MealOrder
    ];

    public static readonly IReadOnlySet<int> Selectable = Ordered.ToHashSet();

    public static string DisplayName(int orderTypeId) => orderTypeId switch
    {
        (int)TransactionTypeEnum.Topup => "Topup",
        (int)TransactionTypeEnum.MealOrder => "Pre-Order Meal",
        _ => "Order Type"
    };
}

public static class MealItemChannelOptionIds
{
    public const int DefaultWhenMissing = (int)TransactionTypeEnum.A_La_Carte;

    public static readonly IReadOnlyList<int> Ordered =
    [
        (int)TransactionTypeEnum.MealOrder,
        (int)TransactionTypeEnum.A_La_Carte,
        (int)TransactionTypeEnum.POS
    ];

    public static readonly IReadOnlyList<int> MenuPair =
    [
        (int)TransactionTypeEnum.MealOrder,
        (int)TransactionTypeEnum.A_La_Carte
    ];

    public static readonly IReadOnlySet<int> Selectable = Ordered.ToHashSet();

    public static string DisplayName(int orderTypeId) => orderTypeId switch
    {
        (int)TransactionTypeEnum.MealOrder => "Pre-Order Meal",
        (int)TransactionTypeEnum.A_La_Carte => "A La Carte",
        (int)TransactionTypeEnum.POS => "POS",
        _ => "Channel"
    };

    /// <summary>String channel ids for Item Master client-side rules (matches &lt;option value&gt;).</summary>
    public static IReadOnlyDictionary<string, string> JavaScriptChannelIds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["MEAL_PLAN"] = OrderTypeJavaScriptIds.StringMap["MEAL_ORDER"],
            ["ALA_CARTE"] = OrderTypeJavaScriptIds.StringMap["ALA_CARTE"],
            ["POS"] = OrderTypeJavaScriptIds.StringMap["POS"]
        };
}
