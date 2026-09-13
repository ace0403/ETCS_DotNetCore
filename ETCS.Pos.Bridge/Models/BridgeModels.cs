using System;
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
    public string CompanyLine { get; set; } = "Emirates Taste Catering Services Food LLC";
    public string VatRegNoLine { get; set; } = "VAT Reg No: 100355890300003";
    public string LocationLine { get; set; } = string.Empty;
    public string TerminalLine { get; set; } = string.Empty;
    public string? LogoBase64 { get; set; }
    public DateTime? PrintedAt { get; set; }
    public bool IsUndo { get; set; }
}

public sealed class ReceiptPrintResult
{
    public bool IsSuccess { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? PreviewImageBase64 { get; set; }
    public string? PreviewFilePath { get; set; }
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

public sealed class NfcWaitCardRequest
{
    public int TimeoutSeconds { get; set; }
}

public sealed class NfcWaitCardResult
{
    public bool IsSuccess { get; set; }
    public string CardSn { get; set; } = string.Empty;
    public string UidHex { get; set; } = string.Empty;
    public string UidHexReversed { get; set; } = string.Empty;
    public string UidDecimal { get; set; } = string.Empty;
    public string UidDecimalReversed { get; set; } = string.Empty;
    public string ReaderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class NfcStatusResult
{
    public bool IsReady { get; set; }
    public string ReaderName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> Readers { get; set; } = Array.Empty<string>();
}
