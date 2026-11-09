using System.Text.Json;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Logging;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository
{
    public async Task<NativePaymentVerificationResult> CancelNativeSdkPaymentAsync(
        NativePaymentVerificationRequest expected, CancellationToken cancellationToken)
    {
        if (!ValidExpected(expected) || _options.NativeConfigurationError("Card") is not null
            || !TryGetMobileMerchant(out var merchant))
            return Pending("Native cancellation configuration is unavailable.", expected);
        var token = await GenerateTokenAsync(true, cancellationToken);
        if (!token.IsSuccess) return Pending("Cancellation could not be authenticated. Please try again.", expected);
        try
        {
            using var timeout = CreateNativeSessionTimeout(cancellationToken);
            // Mirrors the bundled SDK's page cancellation. No Capture, Reversal or Refund.
            using var request = CreateNativeJsonPostRequest(new
            {
                CancelPayment = new { TransactionID = expected.TransactionId, CancelPayment = true,
                    UserName = merchant.UserName, AuthenticationToken = token.AuthenticationToken }
            }, ResolveGatewayBaseUrl(true), merchant);
            request.Headers.Authorization = null;
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Native SDK cancellation unconfirmed. HttpStatus={HttpStatus}", (int)response.StatusCode);
                return Pending("The gateway could not confirm cancellation. Please retry.", expected);
            }
            var t = DeserializeNativeTransactionResponse(await response.Content.ReadAsStringAsync(timeout.Token))?.Transaction;
            var code = t is null ? null : Code(t.ResponseCode);
            // 6801 only permits a fresh read of the ORIGINAL transaction; it is not closure proof.
            if (t is null || code is not ("0" or "6801")
                || (!string.IsNullOrWhiteSpace(t.TransactionID) && t.TransactionID.Trim() != expected.TransactionId)
                || (!string.IsNullOrWhiteSpace(t.OrderID) && t.OrderID.Trim() != expected.OrderId)
                || !OptionalCurrencyMatches(t.Currency, expected.Currency)
                || (Present(t.IsCaptured) && (!TryBoolean(t.IsCaptured, out var captured) || captured))
                || (Present(t.Balance) && (!TryMoney(t.Balance, out var balance) || balance != 0))
                || (Present(t.Amount) && (!TryMoney(t.Amount, out var amount) || (amount != 0 && amount != expected.Amount)))
                || (code == "0" && (t.TransactionID?.Trim() != expected.TransactionId || string.IsNullOrWhiteSpace(t.UniqueID))))
            {
                var safeCode = code is { Length: > 0 and <= 12 } && code.All(char.IsAsciiDigit) ? code : "unavailable";
                _logger.LogWarning("Native SDK cancellation unconfirmed. ResponseCode={ResponseCode}", safeCode);
                return Pending("The gateway could not confirm cancellation. Please retry.", expected);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or JsonException or OperationCanceledException)
        {
            _logger.LogWarning("Native SDK cancellation unconfirmed. ExceptionType={ExceptionType}", ex.GetType().Name);
            return Pending("Cancellation could not be confirmed. The original reference has been kept.", expected);
        }
        // CancelPayment success cannot authorize release. Finalization must prove closure,
        // or captured evidence must send the caller back to complete the original payment.
        return await FinalizeNativePaymentAsync(expected, cancellationToken);
    }
}
