using Microsoft.AspNetCore.Mvc.Rendering;

namespace ETCS.Pos.Web.Models;

public sealed class PosPageViewModel
{
    public IReadOnlyList<SelectListItem> Schools { get; init; } = [];
    public IReadOnlyList<SelectListItem> Terminals { get; init; } = [];
    public string BridgeBaseUrl { get; init; } = string.Empty;
    public bool BridgeSetupAvailable { get; init; }
    public string BridgeSetupDownloadUrl { get; init; } = string.Empty;
    public decimal VatPercent { get; init; }
    public decimal DefaultDiscount { get; init; }
    public bool ApiOnline { get; init; } = true;
    public string ApiStatusMessage { get; init; } = string.Empty;
    public string ApiStatusDetail { get; init; } = string.Empty;
}

public sealed class PosCartItemViewModel
{
    public int ItemId { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string ImageUrl { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public int Quantity { get; init; } = 1;
    public decimal LineTotal => Price * Quantity;
}

public sealed class PosCartViewModel
{
    public IReadOnlyList<PosCartItemViewModel> Items { get; init; } = [];
    public decimal SubTotal => Items.Sum(i => i.LineTotal);
    public decimal DiscountPercent { get; init; }
    public decimal VatPercent { get; init; }
    public decimal DiscountAmount => SubTotal * DiscountPercent / 100m;
    public decimal TotalAfterDiscount => SubTotal - DiscountAmount;
    public decimal VatAmount => TotalAfterDiscount * VatPercent / 100m;
    public decimal GrandTotal => TotalAfterDiscount;
}
