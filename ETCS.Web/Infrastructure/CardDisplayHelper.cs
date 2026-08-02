namespace ETCS.Web.Infrastructure;

public static class CardDisplayHelper
{
    /// <summary>Masks a card number, leaving only the last three characters visible.</summary>
    public static string MaskLastThree(string? cardNumber)
    {
        var card = cardNumber?.Trim() ?? string.Empty;
        if (card.Length == 0)
        {
            return string.Empty;
        }

        if (card.Length <= 3)
        {
            return card;
        }

        return "****" + card[^3..];
    }
}
