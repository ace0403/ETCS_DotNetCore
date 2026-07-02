using System.Collections.Generic;

namespace ETCS.Pos.Bridge.Models;

public sealed class IbonusPurchaseRequest
{
    public string TerminalIp { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

public sealed class IbonusUndoRequest
{
    public string TerminalIp { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string TransactionId { get; set; } = string.Empty;
    public int ItemCount { get; set; }
}

public sealed class IbonusOperationResult
{
    public bool IsSuccess { get; set; }
    public int PosResult { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public ulong BalPrepaidCn { get; set; }
    public ulong AccSpendingCn { get; set; }
}

public sealed class ReceiptLineItem
{
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; } = 1;
}

public sealed class ReceiptPrintRequest
{
    public List<ReceiptLineItem> Items { get; set; } = new();
    public decimal Total { get; set; }
    public decimal VatPercent { get; set; }
    public decimal DiscountPercent { get; set; }
    public bool DiscountApplied { get; set; }
    public string HeaderLine1 { get; set; } = "Harness Foods And";
    public string HeaderLine2 { get; set; } = "Restaurants L.L.C";
    public bool IsUndo { get; set; }
}

public sealed class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string LocalIp { get; set; } = string.Empty;
    public string Service { get; set; } = "ETCSPosBridge";
}

public sealed class IbonusConnectTestResult
{
    public bool IsReachable { get; set; }
    public string Message { get; set; } = string.Empty;
    public string SoapUrl { get; set; } = string.Empty;
    public System.Collections.Generic.IReadOnlyList<string> Details { get; set; } = System.Array.Empty<string>();
}
