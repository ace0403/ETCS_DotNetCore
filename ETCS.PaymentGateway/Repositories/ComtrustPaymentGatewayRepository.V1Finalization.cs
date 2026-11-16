using System.Text.Json;
using ETCS.PaymentGateway.Models;
using Microsoft.Extensions.Logging;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository
{
    private PaymentCaptureResult ValidateV1Finalization(string raw, PaymentCaptureRequest expected, bool httpSuccess)
    {
        PaymentCaptureResult Result(string status, string message, bool success = false, bool pending = false) => new()
        {
            TransactionId = expected.TransactionId,
            Status = status,
            Message = message,
            IsSuccess = success,
            IsPending = pending
        };

        PaymentCaptureResult Unverified(string reason)
        {
            _logger.LogWarning(
                "V1 Finalization validation failed. TraceId={TraceId}; Reason={Reason}.",
                System.Diagnostics.Activity.Current?.TraceId.ToString(),
                reason);
            return Result("status-unconfirmed", V1GatewayMessages.Unconfirmed);
        }

        try
        {
            if (string.IsNullOrWhiteSpace(raw))
                return Unverified("empty-response");

            using var document = JsonDocument.Parse(raw);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return Unverified("invalid-envelope");

            RejectDuplicateFields(root, ["Transaction"]);
            var transaction = V1Field(root, "Transaction");
            if (transaction.ValueKind != JsonValueKind.Object)
                return Unverified("missing-transaction");

            RejectDuplicateFields(transaction, [
                "ResponseCode", "TransactionID", "OrderID", "Amount",
                "Balance", "Currency", "IsCaptured", "ResponseClassDescription"
            ]);
            foreach (var name in new[] { "Amount", "Balance" })
            {
                var money = V1Field(transaction, name);
                if (money.ValueKind == JsonValueKind.Object)
                    RejectDuplicateFields(money, ["Value"]);
            }

            var t = transaction.Deserialize<NativeEpgTransaction>(ResponseJsonOptions)!;
            var code = Code(t.ResponseCode);
            var classField = V1Field(transaction, "ResponseClassDescription");
            var responseClass = classField.ValueKind == JsonValueKind.String
                ? classField.GetString()?.Trim().ToLowerInvariant()
                : string.Empty;

            if (!httpSuccess)
                return Unverified("http-failure");
            if (string.IsNullOrWhiteSpace(expected.TransactionId) || string.IsNullOrWhiteSpace(expected.OrderId))
                return Unverified("missing-original-reference");

            // Finalization need not echo TransactionID/OrderID. Reject only contradictory echoes.
            var hasTransactionId = !string.IsNullOrWhiteSpace(t.TransactionID);
            var hasOrderId = !string.IsNullOrWhiteSpace(t.OrderID);
            if ((hasTransactionId && t.TransactionID!.Trim() != expected.TransactionId)
                || (hasOrderId && t.OrderID!.Trim() != expected.OrderId))
                return Unverified("reference-mismatch");

            if (code == "6824")
                return Result("cancelled-confirmed", "Payment cancelled by the gateway.");

            if (code is null)
                return Unverified("missing-response-code");

            if (code != "0")
            {
                // Re-finalization of an already-captured hosted payment can return a non-zero
                // code while still describing a successful request. Accept only with explicit
                // success/closed class and no outstanding balance (IsCaptured may stay False on CPT:Y).
                if (IsPaidSuccessClass(responseClass)
                    && IsPaidSuccessDescription(t.ResponseDescription)
                    && TryReadCapturedEvidence(t, out _, out var reBalance)
                    && reBalance is not > 0
                    && !NativeEpgTransport.RequiresManualCapture(_options.TransactionHint))
                {
                    return Result("captured", "Payment captured and verified.", success: true);
                }

                if (V1GatewayMessages.IsUnconfirmed(t.ResponseDescription))
                    return Unverified("gateway-unconfirmed");

                return Result(
                    responseClass ?? string.Empty,
                    string.IsNullOrWhiteSpace(t.ResponseDescription)
                        ? "Payment could not be confirmed."
                        : t.ResponseDescription,
                    pending: responseClass == "pending" && (hasTransactionId || hasOrderId));
            }

            if (responseClass == "pending")
                return Result("pending", "Payment is still processing.", pending: true);

            // Empty ResponseClass is common on Finalization; do not reject it when code is 0.
            if (!string.IsNullOrEmpty(responseClass) && !IsPaidSuccessClass(responseClass))
                return Unverified("conflicting-response-class");

            // Amount is optional on Finalization. Validate only when the gateway returns it.
            if (Present(t.Amount))
            {
                if (!TryMoney(t.Amount, out var amount) || amount <= 0
                    || (expected.ExpectedAmount is > 0 && amount != expected.ExpectedAmount.Value))
                    return Unverified("charged-amount-mismatch");
            }

            if (!OptionalCurrencyMatches(t.Currency, _options.Currency))
                return Unverified("currency-mismatch");

            decimal? balance = null;
            if (Present(t.Balance))
            {
                if (!TryMoney(t.Balance, out var value) || value < 0)
                    return Unverified("invalid-balance");
                balance = value;
            }

            bool? captured = null;
            if (Present(t.IsCaptured))
            {
                if (!TryBoolean(t.IsCaptured, out var flag))
                    return Unverified("invalid-capture-flag");
                captured = flag;
            }

            // Hosted CPT:Y Finalization often returns Success + Balance 0 with IsCaptured=False
            // and an empty CaptureID (auto-capture, no separate Capture call). Do not treat
            // IsCaptured=False alone as auth-only when there is no outstanding balance.
            if (captured == true && balance is > 0)
                return Unverified("inconsistent-capture");

            var manualCapture = NativeEpgTransport.RequiresManualCapture(_options.TransactionHint);
            if (balance is > 0)
                return Result("authorized", "Payment is authorized but capture is not confirmed.", pending: true);

            if (captured == false && manualCapture)
                return Result("authorized", "Payment is authorized but capture is not confirmed.", pending: true);

            // CPT:Y (or missing CPT): code 0 + success/closed + zero/absent balance = captured.
            if (captured != true && balance != 0 && manualCapture)
                return Unverified("capture-not-confirmed");

            return Result("captured", "Payment captured and verified.", success: true);
        }
        catch (JsonException)
        {
            return Unverified("invalid-response");
        }
        catch (InvalidOperationException)
        {
            return Unverified("invalid-response");
        }
    }

    private static bool IsPaidSuccessClass(string? responseClass) =>
        responseClass is "success" or "closed";

    private static bool IsPaidSuccessDescription(string? description) =>
        !string.IsNullOrWhiteSpace(description)
        && description.Contains("processed successfully", StringComparison.OrdinalIgnoreCase);

    private static bool TryReadCapturedEvidence(
        NativeEpgTransaction t,
        out bool? captured,
        out decimal? balance)
    {
        captured = null;
        balance = null;

        if (Present(t.Balance))
        {
            if (!TryMoney(t.Balance, out var value) || value < 0)
                return false;
            balance = value;
        }

        if (Present(t.IsCaptured))
        {
            if (!TryBoolean(t.IsCaptured, out var flag))
                return false;
            captured = flag;
        }

        return true;
    }

    private static JsonElement V1Field(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value;
        }

        return default;
    }
}
