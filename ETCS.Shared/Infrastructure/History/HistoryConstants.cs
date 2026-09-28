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

    /// <summary>Manual topup (not POS purchase detail)</summary>
    public const int TopupManual = 21004;

    private static readonly HashSet<int> PosAccessLogTypes =
    [
        PosCashPurchase,
        PosCreditCardPurchase,
        PosCardPurchase
    ];

    public static bool IsPosAccessLogType(int? transactionType) =>
        transactionType.HasValue && PosAccessLogTypes.Contains(transactionType.Value);

    public static string ResolvePosTypeLabel(int? transactionType) => transactionType switch
    {
        PosCashPurchase => "Cash Purchase",
        PosCreditCardPurchase => "Credit Card Purchase",
        PosCardPurchase => "Card Purchase",
        _ => "POS"
    };
}
