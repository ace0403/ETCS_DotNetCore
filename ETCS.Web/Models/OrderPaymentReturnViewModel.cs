using ETCS.Shared.Application.Orders.Summaries;

namespace ETCS.Web.Models;

public sealed class OrderPaymentReturnViewModel : OrderPaymentReceiptDto
{
    public static OrderPaymentReturnViewModel FromReceipt(OrderPaymentReceiptDto receipt) =>
        new()
        {
            IsSuccess = receipt.IsSuccess,
            IsPending = receipt.IsPending,
            Message = receipt.Message,
            OrderId = receipt.OrderId,
            OrderTypeId = receipt.OrderTypeId,
            AlaCarteSummary = receipt.AlaCarteSummary,
            ComboSummary = receipt.ComboSummary
        };
}
