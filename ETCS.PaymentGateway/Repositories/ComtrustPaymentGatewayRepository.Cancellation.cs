using ETCS.PaymentGateway.Models;
using System.Globalization;

namespace ETCS.PaymentGateway.Repositories;

public sealed partial class ComtrustPaymentGatewayRepository
{
    public async Task<NativePaymentVerificationResult> ReverseNativePaymentAsync(NativePaymentVerificationRequest expected,
        NativePaymentVerificationResult authorization, CancellationToken cancellationToken)
    {
        if (!ValidExpected(expected) || !expected.ManualCapture || !authorization.IsAuthorized || authorization.IsCaptured
            || authorization.IsSuccess || authorization.IsPending || authorization.ResponseCode != "0"
            || authorization.OrderId != expected.OrderId || authorization.TransactionId != expected.TransactionId
            || authorization.Amount != expected.Amount || authorization.Currency != expected.Currency
            || authorization.RemainingBalance != expected.Amount)
            return Invalid("Cancellation requires a verified full, uncaptured manual authorization.", expected);
        var reply = await SendNativeOperationAsync(new
        {
            // Request the FULL registered amount. A partial reversal cannot authorize a new payment.
            Reversal = new { Customer = _options.Mobile.CustomerName, TransactionID = expected.TransactionId,
                Amount = expected.Amount.ToString("0.00", CultureInfo.InvariantCulture), Currency = expected.Currency,
                Store = NullIfWhiteSpace(_options.Mobile.Store), Terminal = NullIfWhiteSpace(_options.Mobile.Terminal) }
        }, cancellationToken);
        var failed = CheckReply(reply.Transaction, reply.HttpSuccess, expected);
        if (failed is not null) return failed;
        var t = reply.Transaction!;
        if (Present(t.Amount) && (!TryMoney(t.Amount, out var reported) || (reported != 0 && reported != expected.Amount)))
            return Invalid("Reversal returned inconsistent amount metadata.", expected);
        if (!TryMoney(t.Balance, out var balance) || balance != 0 || string.IsNullOrWhiteSpace(t.UniqueID)
            || (Present(t.IsCaptured) && (!TryBoolean(t.IsCaptured, out var captured) || captured)))
            return Pending("The full reversal could not be confirmed. Keep the original payment for reconciliation.", expected);
        return new() { IsSuccess = true, IsCancelled = true, Status = "cancelled", ResponseCode = "0",
            OrderId = expected.OrderId, TransactionId = expected.TransactionId, Amount = expected.Amount,
            Currency = expected.Currency, RemainingBalance = 0, UniqueId = t.UniqueID,
            Message = "The original authorization has been reversed." };
    }
}
