using System.Text.Json;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository
{
    public async Task<NativePaymentSessionResult> ResumeNativeCardSessionAsync(
        NativePaymentVerificationRequest expected, string returnUrl, CancellationToken cancellationToken)
    {
        if (!ValidExpected(expected) || _options.NativeConfigurationError("Card") is not null || !TryGetMobileMerchant(out var merchant))
            return FailNative(expected.OrderId, expected.Amount, "Native payment session configuration is unavailable.");
        var token = await GenerateTokenAsync(true, cancellationToken);
        if (!token.IsSuccess) return FailNative(expected.OrderId, expected.Amount, token.Message);
        try
        {
            using var timeout = CreateNativeSessionTimeout(cancellationToken);
            var baseUrl = ResolveGatewayBaseUrl(true);
            // Mirror the SDK's read-only page request. Reuse the original transaction;
            // never register, authorize, finalize or capture as part of resuming the UI.
            using var request = CreateNativeJsonPostRequest(new
            {
                PaymentDataInApp = new { TransactionID = expected.TransactionId, UserName = merchant.UserName,
                    AuthenticationToken = token.AuthenticationToken, Customer = merchant.CustomerName, Language = "en" }
            }, baseUrl, merchant);
            request.Headers.Authorization = null; // This SDK operation authenticates with its token.
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(timeout.Token));
            if (!response.IsSuccessStatusCode || document.RootElement.ValueKind != JsonValueKind.Object)
                return FailNative(expected.OrderId, expected.Amount, "The original card session could not be loaded. Please retry.");
            RejectDuplicateFields(document.RootElement, ["PaymentDataInApp"]);
            var envelope = document.RootElement.EnumerateObject()
                .SingleOrDefault(p => p.Name.Equals("PaymentDataInApp", StringComparison.OrdinalIgnoreCase)).Value;
            if (envelope.ValueKind != JsonValueKind.Object)
                return FailNative(expected.OrderId, expected.Amount, "Gateway returned an invalid card session response.");
            RejectDuplicateFields(envelope, ["ResponseCode", "TransactionID", "OrderID"]);
            var page = envelope.Deserialize<NativeEpgTransaction>(ResponseJsonOptions);
            if (page is null || Code(page.ResponseCode) != "0")
                return FailNative(expected.OrderId, expected.Amount, "The original card session is unavailable. Please confirm its status or contact support.");
            if ((!string.IsNullOrWhiteSpace(page.TransactionID) && page.TransactionID.Trim() != expected.TransactionId)
                || (!string.IsNullOrWhiteSpace(page.OrderID) && page.OrderID.Trim() != expected.OrderId))
                return FailNative(expected.OrderId, expected.Amount, "Gateway returned a different card session.");
            return new NativePaymentSessionResult
            {
                IsSuccess = true, Message = "Original card payment is ready to resume.",
                OrderId = expected.OrderId, TransactionId = expected.TransactionId, Amount = expected.Amount,
                Currency = expected.Currency, ManualCapture = expected.ManualCapture, PaymentMethod = "Card",
                AuthenticationToken = token.AuthenticationToken, MerchantUserName = merchant.UserName,
                CustomerName = merchant.CustomerName, BaseUrl = ToNativeSdkBaseUrl(baseUrl),
                CallbackUrl = ResolveNativeReturnUrl(expected.OrderId, returnUrl)
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            return FailNative(expected.OrderId, expected.Amount, "The original card session could not be loaded. Please retry confirmation.");
        }
    }
}
