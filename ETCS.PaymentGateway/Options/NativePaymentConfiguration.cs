using ETCS.PaymentGateway.Models;

namespace ETCS.PaymentGateway.Options;

public static class NativePaymentConfiguration
{
    public static string? NativeConfigurationError(this PaymentGatewayOptions options, string paymentMethod)
    {
        if (options.Mobile is null || !options.Mobile.IsConfigured || string.IsNullOrWhiteSpace(options.Mobile.CustomerName))
            return "Native payment merchant credentials and customer name must be configured.";
        var baseUrl = string.IsNullOrWhiteSpace(options.Mobile.BaseUrl) ? options.BaseUrl : options.Mobile.BaseUrl;
        if (!Uri.TryCreate(baseUrl?.Trim(), UriKind.Absolute, out var gateway) || gateway.Scheme != Uri.UriSchemeHttps
            || gateway.UserInfo.Length != 0 || gateway.Fragment.Length != 0 || gateway.Query.Length != 0 || gateway.AbsolutePath != "/")
            return "Native payments require an HTTPS gateway base URL without a path, query or user information.";
        if (options.Currency is not { Length: 3 } || !options.Currency.All(c => c is >= 'A' and <= 'Z'))
            return "Native payment currency must be a three-letter uppercase currency code.";
        var isWallet = paymentMethod is "ApplePay" or "SamsungPay";
        if (paymentMethod is not ("Card" or "ApplePay" or "SamsungPay")) return "Unsupported native payment method.";
        if (isWallet && options.Currency != "AED") return "Native wallet payments require AED.";
        if (paymentMethod == "ApplePay" && string.IsNullOrWhiteSpace(options.ApplePayMerchantIdentifier))
            return "Apple Pay merchant identifier is not configured.";
        if (paymentMethod == "SamsungPay" && (string.IsNullOrWhiteSpace(options.SamsungPayMerchantId)
            || string.IsNullOrWhiteSpace(options.SamsungPayServiceId))) return "Samsung Pay merchant configuration is incomplete.";
        try { NativeEpgTransport.RequiresManualCapture(isWallet ? options.WalletTransactionHint : options.TransactionHint); }
        catch (InvalidOperationException ex) { return ex.Message; }
        return null;
    }
}
