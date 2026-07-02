using System.Globalization;
using ETCS.Shared.Infrastructure.Orders;

namespace ETCS.Shared.Application.Email;

public static class OrderEmailHelper
{
    public static string BuildOrderItemsHtml(IReadOnlyList<OrderDetailLineItemDto>? items)
    {
        if (items is null || items.Count == 0)
        {
            return string.Empty;
        }

        var lines = items.Select(item =>
            $"- {item.MealDate:dd-MM-yyyy} | {item.ItemName} | {item.ItemPrice.ToString("F2", CultureInfo.InvariantCulture)}");

        return string.Join("<br/>", lines);
    }
}
