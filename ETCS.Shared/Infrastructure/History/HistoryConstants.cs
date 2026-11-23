namespace ETCS.Shared.Infrastructure.History;

public static class HistoryConstants
{
    public static readonly DateTime LegacyCutoffDate = new(2026, 8, 15);

    public const int AccessLogMealOrderType = 9001;

    /// <summary>Cash Purchase</summary>
    public const int PosCashPurchase = 1004;

    /// <summary>Credit Card Purchase</summary>
    public const int PosCreditCardPurchase = 2004;

    /// <summary>Card Purchase (student card / NFC)</summary>
    public const int PosCardPurchase = 21002;

    /// <summary>Undo Card Purchase</summary>
    public const int PosUndoPurchase = 21006;

    /// <summary>Manual topup (not POS purchase detail)</summary>
    public const int TopupManual = 21004;

    /// <summary>AccessLog CustomerID used by legacy cash purchase SP.</summary>
    public const string PosCashCustomerId = "208";

    /// <summary>AccessLog CustomerID used by legacy credit-card purchase SP.</summary>
    public const string PosCardCustomerId = "209";

    private static readonly HashSet<int> PosAccessLogTypes =
    [
        PosCashPurchase,
        PosCreditCardPurchase,
        PosCardPurchase
    ];

    private static readonly HashSet<int> PosReprintSearchTypes =
    [
        PosCashPurchase,
        PosCreditCardPurchase,
        PosCardPurchase,
        PosUndoPurchase
    ];

    public static bool IsPosAccessLogType(int? transactionType) =>
        transactionType.HasValue && PosAccessLogTypes.Contains(transactionType.Value);

    public static bool IsPosReprintSearchType(int? transactionType) =>
        transactionType.HasValue && PosReprintSearchTypes.Contains(transactionType.Value);

    public static string ResolvePosTypeLabel(int? transactionType) => transactionType switch
    {
        PosCashPurchase => "Cash Purchase",
        PosCreditCardPurchase => "Credit Card Purchase",
        PosCardPurchase => "Card Purchase",
        PosUndoPurchase => "Undo Purchase",
        _ => "POS"
    };

    public static string ResolvePosPaymentModeLabel(int? transactionType) => transactionType switch
    {
        PosCashPurchase => "Cash",
        PosCreditCardPurchase => "Card",
        PosCardPurchase => "Student Card",
        PosUndoPurchase => "Undo",
        _ => "Unknown"
    };
}
