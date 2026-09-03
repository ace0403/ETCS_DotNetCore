namespace ETCS.Shared.Enumeration;

public enum PaymentMethodEnum : int
{
    Unknown = 0,
    Card = 1,
    ApplePay = 2,
    SamsungPay = 3,
    WebRedirect = 4
}

public static class PaymentMethodEnumExtensions
{
    public static PaymentMethodEnum ParseOrUnknown(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PaymentMethodEnum.Unknown;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "card" => PaymentMethodEnum.Card,
            "applepay" => PaymentMethodEnum.ApplePay,
            "samsungpay" => PaymentMethodEnum.SamsungPay,
            "webredirect" or "web" => PaymentMethodEnum.WebRedirect,
            _ => PaymentMethodEnum.Unknown
        };
    }

    public static string ToApiString(this PaymentMethodEnum method) =>
        method switch
        {
            PaymentMethodEnum.Card => "Card",
            PaymentMethodEnum.ApplePay => "ApplePay",
            PaymentMethodEnum.SamsungPay => "SamsungPay",
            PaymentMethodEnum.WebRedirect => "WebRedirect",
            _ => "Unknown"
        };
}
