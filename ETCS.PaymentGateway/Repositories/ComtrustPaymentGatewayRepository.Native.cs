using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ETCS.PaymentGateway.Models;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository
{
    private CancellationTokenSource CreateNativeSessionTimeout(CancellationToken parent)
    {
        var source = CancellationTokenSource.CreateLinkedTokenSource(parent);
        source.CancelAfter(TimeSpan.FromSeconds(_options.SessionTimeoutSeconds > 0 ? _options.SessionTimeoutSeconds : 60));
        return source;
    }

    // EPG Registration can return generic Amount.Value=0 before payer processing. This is not capture proof.
    // Persist the requested amount; Finalization must later prove the full charged amount.
    private static bool RegistrationResponseMatches(NativeEpgTransaction transaction, string orderId, decimal amount, string currency) =>
        (transaction.OrderID is null || transaction.OrderID == orderId)
        && (!Present(transaction.Amount) || (TryMoney(transaction.Amount, out var registeredAmount) && (registeredAmount == 0 || registeredAmount == amount)))
        && OptionalCurrencyMatches(transaction.Currency, currency);

    private static NativeEpgResponse? DeserializeValidatedNativeResponse(string raw)
    {
        using var document = JsonDocument.Parse(raw);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Invalid EPG envelope.");
        RejectDuplicateFields(document.RootElement, ["Transaction"]);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!property.Name.Equals("Transaction", StringComparison.OrdinalIgnoreCase)
                || property.Value.ValueKind != JsonValueKind.Object) continue;
            RejectDuplicateFields(property.Value, ["ResponseCode", "TransactionID", "OrderID", "Amount", "Currency", "Balance",
                "IsCaptured", "AuthenticationToken", "SessionID", "ApprovalCode", "UniqueID"]);
            foreach (var money in property.Value.EnumerateObject())
                if ((money.Name.Equals("Amount", StringComparison.OrdinalIgnoreCase) || money.Name.Equals("Balance", StringComparison.OrdinalIgnoreCase))
                    && money.Value.ValueKind == JsonValueKind.Object) RejectDuplicateFields(money.Value, ["Value"]);
        }
        return document.RootElement.Deserialize<NativeEpgResponse>(ResponseJsonOptions);
    }

    private static void RejectDuplicateFields(JsonElement element, string[] protectedFields)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
            if (protectedFields.Contains(property.Name, StringComparer.OrdinalIgnoreCase) && !seen.Add(property.Name))
                throw new JsonException("Ambiguous duplicate EPG payment field.");
    }

    private HttpRequestMessage CreateNativeJsonPostRequest<T>(T payload, string baseUrl, MobileMerchant merchant)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != Uri.UriSchemeHttps
            || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0 || endpoint.Query.Length != 0 || endpoint.AbsolutePath != "/")
            throw new InvalidOperationException("Native gateway requests require HTTPS.");
        var body = JsonSerializer.SerializeToNode(payload, RequestJsonOptions)!;
        // Keep HTTP Basic authentication. Some EPG endpoints (including the tested
        // sandbox) also require body credentials; select this explicitly per mobile merchant.
        // GenerateToken retains its existing body credentials in either mode.
        foreach (var operation in new[] { "Registration", "WalletRegistration", "Finalization", "Capture", "Reversal" })
            if (body[operation] is JsonObject details)
            {
                if (_options.Mobile.IncludeCredentialsInBody)
                {
                    details["UserName"] = merchant.UserName;
                    details["Password"] = merchant.Password;
                }
                else
                {
                    details.Remove("UserName");
                    details.Remove("Password");
                }
            }
        var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(body.ToJsonString(RequestJsonOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes($"{merchant.UserName}:{merchant.Password}")));
        // Native operations are retried by their stateful coordinator, never blindly by Polly.
        request.Options.Set(NativeEpgTransport.RequestMarker, true);
        return request;
    }

    public async Task<NativePaymentVerificationResult> FinalizeNativePaymentAsync(
        NativePaymentVerificationRequest expected, CancellationToken cancellationToken)
    {
        if (!ValidExpected(expected)) return Invalid("Stored native payment details are incomplete.");
        var reply = await SendNativeOperationAsync(new
        {
            Finalization = new { TransactionID = expected.TransactionId, Customer = _options.Mobile.CustomerName,
                Store = NullIfWhiteSpace(_options.Mobile.Store), Terminal = NullIfWhiteSpace(_options.Mobile.Terminal) }
        }, cancellationToken);
        var failed = CheckReply(reply.Transaction, reply.HttpSuccess, expected, allowTerminalDecline: true);
        if (failed is not null) return failed;
        var t = reply.Transaction!;
        if (t.OrderID?.Trim() != expected.OrderId
            || !TryMoney(t.Amount, out var amount) || amount != expected.Amount)
            return Invalid("Gateway order or charged amount does not match the stored payment.", expected);
        if (!OptionalCurrencyMatches(t.Currency, expected.Currency))
            return Invalid("Gateway currency does not match the registered currency.", expected);

        decimal? balance = null;
        if (Present(t.Balance))
        {
            if (!TryMoney(t.Balance, out var parsedBalance) || parsedBalance < 0)
                return Invalid("Gateway returned an invalid outstanding balance.", expected);
            balance = parsedBalance;
        }
        bool? captured = null;
        if (Present(t.IsCaptured))
        {
            if (!TryBoolean(t.IsCaptured, out var flag))
                return Invalid("Gateway returned an invalid capture flag.", expected);
            captured = flag;
        }

        if (captured == true)
        {
            if (balance is > 0) return Invalid("Gateway capture flag conflicts with the outstanding balance.", expected);
            return Verified(expected, t, balance);
        }
        if (captured == false || expected.ManualCapture || balance is > 0)
        {
            // Do not capture an unknown or partially captured balance a second time.
            if (balance != expected.Amount)
                return Pending("Authorization exists, but the full outstanding balance needs reconciliation.", expected);
            return new NativePaymentVerificationResult
            {
                IsAuthorized = true, Status = "authorized", ResponseCode = "0",
                Message = "Payment authorized; capture is required.", OrderId = expected.OrderId,
                TransactionId = expected.TransactionId, Amount = expected.Amount, Currency = expected.Currency,
                RemainingBalance = balance, ApprovalCode = t.ApprovalCode, UniqueId = t.UniqueID
            };
        }

        // With persisted CPT:Y terms, ResponseCode=0 Finalization is authorization + auto-capture.
        // An explicit false flag or positive balance above always takes precedence.
        return Verified(expected, t, balance);
    }

    public async Task<NativePaymentVerificationResult> CaptureNativePaymentAsync(
        NativePaymentVerificationRequest expected, NativePaymentVerificationResult authorization,
        CancellationToken cancellationToken)
    {
        if (!ValidExpected(expected) || !authorization.IsAuthorized || authorization.IsCaptured
            || authorization.ResponseCode != "0" || authorization.OrderId != expected.OrderId
            || authorization.TransactionId != expected.TransactionId || authorization.Amount != expected.Amount
            || authorization.Currency != expected.Currency || authorization.RemainingBalance != expected.Amount)
            return Invalid("Capture requires a matching, fully validated authorization.", expected);

        var reply = await SendNativeOperationAsync(new
        {
            Capture = new
            {
                Customer = _options.Mobile.CustomerName, TransactionID = expected.TransactionId,
                Amount = expected.Amount.ToString("0.00", CultureInfo.InvariantCulture), Currency = expected.Currency,
                Store = NullIfWhiteSpace(_options.Mobile.Store), Terminal = NullIfWhiteSpace(_options.Mobile.Terminal)
            }
        }, cancellationToken);
        var failed = CheckReply(reply.Transaction, reply.HttpSuccess, expected);
        if (failed is not null) return failed;
        var t = reply.Transaction!;
        if (!string.IsNullOrWhiteSpace(t.OrderID) && t.OrderID.Trim() != expected.OrderId)
            return Invalid("Capture returned a different order.", expected);
        if (!OptionalCurrencyMatches(t.Currency, expected.Currency))
            return Invalid("Capture returned a different currency.", expected);
        if (!TryMoney(t.Balance, out var remaining) || remaining != 0)
            return Pending("Capture has not confirmed a zero outstanding balance. Reconciliation is required.", expected);
        if (Present(t.IsCaptured) && (!TryBoolean(t.IsCaptured, out var captured) || !captured))
            return Invalid("Capture response does not confirm capture.", expected);

        // EPG's Capture example returns Amount.Value=0: this field is NOT compared
        // with the purchase amount. The validated authorization + requested capture amount
        // + ResponseCode=0 + zero remaining balance establish the captured amount.
        return Verified(expected, t, 0, authorization.ApprovalCode);
    }

    private async Task<(NativeEpgTransaction? Transaction, bool HttpSuccess)> SendNativeOperationAsync<T>(
        T payload, CancellationToken cancellationToken)
    {
        if (!TryGetMobileMerchant(out var merchant)) return (null, false);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.CaptureTimeoutSeconds > 0 ? _options.CaptureTimeoutSeconds : 90));
        try
        {
            using var request = CreateNativeJsonPostRequest(payload, ResolveGatewayBaseUrl(true), merchant);
            using var response = await _httpClient.SendAsync(request, timeout.Token);
            var raw = await response.Content.ReadAsStringAsync(timeout.Token);
            return (DeserializeNativeTransactionResponse(raw)?.Transaction, response.IsSuccessStatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or OperationCanceledException or JsonException)
        {
            _logger.LogWarning(ex, "Native EPG response is unconfirmed; no ledger completion is permitted.");
            return (null, false);
        }
    }

    private static NativePaymentVerificationResult? CheckReply(
        NativeEpgTransaction? transaction, bool httpSuccess, NativePaymentVerificationRequest expected, bool allowTerminalDecline = false)
    {
        if (!httpSuccess || transaction is null)
            return Pending("Gateway response is unconfirmed. Please retry confirmation.", expected);
        var code = Code(transaction.ResponseCode);
        if (code is null) return Invalid("Gateway response is missing a valid ResponseCode.", expected);
        if ((!string.IsNullOrWhiteSpace(transaction.TransactionID) && transaction.TransactionID.Trim() != expected.TransactionId)
            || (!string.IsNullOrWhiteSpace(transaction.OrderID) && transaction.OrderID.Trim() != expected.OrderId)
            || !OptionalCurrencyMatches(transaction.Currency, expected.Currency))
            return Invalid("Gateway returned different payment references or currency.", expected);
        // Confirmed against the bundled SDK's CancelPayment flow: Finalization 6824
        // means Transaction Cancelled by Payer. It is terminal NON-PAYMENT, never success.
        // Do not infer closure from 6801 (already cancelled) or a missing page alone.
        if (allowTerminalDecline && code == "6824")
        {
            if (Present(transaction.IsCaptured) && (!TryBoolean(transaction.IsCaptured, out var flag) || flag))
                return Invalid("Cancellation conflicts with the gateway capture flag.", expected);
            decimal? cancelledBalance = null;
            if (Present(transaction.Balance))
            {
                if (!TryMoney(transaction.Balance, out var balance) || balance != 0)
                    return Pending("Cancellation has an unconfirmed outstanding balance. Reconciliation is required.", expected);
                cancelledBalance = balance;
            }
            if (Present(transaction.Amount) && (!TryMoney(transaction.Amount, out var amount) || (amount != 0 && amount != expected.Amount)))
                return Invalid("Cancellation returned inconsistent amount metadata.", expected);
            return new() { IsCancelled = true, Status = "cancelled", ResponseCode = code,
                OrderId = expected.OrderId, TransactionId = expected.TransactionId, Amount = expected.Amount,
                Currency = expected.Currency, RemainingBalance = cancelledBalance, UniqueId = transaction.UniqueID,
                Message = "Payment was cancelled by the payer." };
        }
        if (code != "0" && allowTerminalDecline && string.Equals(transaction.ResponseDescription?.Trim(),
            "Payment Page Query state not found", StringComparison.OrdinalIgnoreCase))
            return new NativePaymentVerificationResult
            {
                IsPending = true, Status = "awaiting-sdk", ResponseCode = code,
                Message = "The card session is not ready for confirmation. Resume the original payment before paying again.",
                OrderId = expected.OrderId, TransactionId = expected.TransactionId
            };
        if (code == "0") return null;
        var pending = code is "210" or "6000" or "6002";
        var declined = allowTerminalDecline && code is "5" or "51";
        return new NativePaymentVerificationResult
        {
            IsPending = pending, ResponseCode = code,
            Status = pending ? "pending" : declined ? "declined" : "failed",
            Message = declined ? "Payment was declined by the bank. Please choose another payment method."
                : transaction.ResponseDescription ?? "Gateway did not approve this operation.",
            OrderId = expected.OrderId, TransactionId = expected.TransactionId
        };
    }

    private static bool ValidExpected(NativePaymentVerificationRequest expected) =>
        !string.IsNullOrWhiteSpace(expected.OrderId) && !string.IsNullOrWhiteSpace(expected.TransactionId)
        && expected.Amount > 0 && decimal.Round(expected.Amount, 2) == expected.Amount
        && expected.Currency is { Length: 3 } && expected.Currency.All(c => c is >= 'A' and <= 'Z');

    private static bool Present(JsonElement value) => value.ValueKind is not (JsonValueKind.Undefined or JsonValueKind.Null);
    private static string? Code(JsonElement value) =>
        value.ValueKind == JsonValueKind.String ? value.GetString()?.Trim()
        : value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)
            ? number.ToString(CultureInfo.InvariantCulture) : null;

    private static bool TryMoney(JsonElement value, out decimal amount)
    {
        amount = 0;
        if (value.ValueKind == JsonValueKind.Object)
        {
            if (!value.TryGetProperty("Value", out var inner)) return false;
            value = inner;
        }
        return value.ValueKind == JsonValueKind.Number ? value.TryGetDecimal(out amount)
            : value.ValueKind == JsonValueKind.String
                && decimal.TryParse(value.GetString(), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture, out amount);
    }

    private static bool TryBoolean(JsonElement value, out bool flag)
    {
        flag = false;
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { flag = value.GetBoolean(); return true; }
        return value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out flag);
    }

    private static bool OptionalCurrencyMatches(JsonElement value, string expected) =>
        !Present(value) || (value.ValueKind == JsonValueKind.String
            && value.GetString()?.Trim().ToUpperInvariant() == expected);

    private static NativePaymentVerificationResult Verified(NativePaymentVerificationRequest expected,
        NativeEpgTransaction transaction, decimal? balance, string? approvalFallback = null) => new()
    {
        IsSuccess = true, IsCaptured = true, Status = "captured", ResponseCode = "0",
        Message = "Gateway payment captured and verified.", OrderId = expected.OrderId,
        TransactionId = expected.TransactionId, Amount = expected.Amount, Currency = expected.Currency,
        RemainingBalance = balance, ApprovalCode = transaction.ApprovalCode ?? approvalFallback, UniqueId = transaction.UniqueID
    };

    private static NativePaymentVerificationResult Invalid(string message, NativePaymentVerificationRequest? expected = null) => new()
    {
        Status = "invalid-response", Message = message, OrderId = expected?.OrderId ?? string.Empty,
        TransactionId = expected?.TransactionId ?? string.Empty
    };
    private static NativePaymentVerificationResult Pending(string message, NativePaymentVerificationRequest expected) => new()
    {
        IsPending = true, Status = "pending", Message = message, OrderId = expected.OrderId, TransactionId = expected.TransactionId
    };
}
