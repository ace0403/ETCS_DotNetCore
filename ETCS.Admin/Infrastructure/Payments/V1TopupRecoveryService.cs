using ETCS.PaymentGateway.Options;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Infrastructure.Audit;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.Extensions.Options;

namespace ETCS.Admin.Infrastructure.Payments;

public sealed class V1TopupRecoveryService(
    IV1TopupRecoveryRepository repository,
    ITransactionRepository transactions,
    ITopupPaymentCompleteService completion,
    AdminPaymentLogBuffer logs,
    IAuditLogWriter audit,
    IOptions<V1TopupRecoveryOptions> recoveryOptions,
    IOptions<PaymentGatewayOptions> gatewayOptions,
    ILogger<V1TopupRecoveryService> logger)
{
    public static bool IsConfigured(V1TopupRecoveryOptions recovery, PaymentGatewayOptions gateway) =>
        recovery.Enabled && Uri.TryCreate(gateway.BaseUrl, UriKind.Absolute, out var uri) &&
        uri.Scheme == Uri.UriSchemeHttps &&
        !string.IsNullOrWhiteSpace(gateway.CustomerName) &&
        !string.IsNullOrWhiteSpace(gateway.UserName) && !string.IsNullOrWhiteSpace(gateway.Password);

    public bool Enabled => IsConfigured(recoveryOptions.Value, gatewayOptions.Value);

    public async Task<V1TopupRecoveryResult> RetryAsync(int id, IReadOnlyList<int>? schoolIds, CancellationToken ct)
    {
        if (!Enabled) return new(false, true, "disabled", "Recovery is not configured. Contact the administrator.");
        var row = await repository.GetAsync(id, schoolIds, ct);
        if (row is null) return new(false, true, "not-found", "Payment was not found in your school access.");
        if (row.ReviewReason is { } reason) return new(false, false, "review", reason);
        // CompleteAsync resolves by references; verify that lookup still identifies the
        // SAME database row, rather than a newer row sharing an old reference.
        var state = await transactions.GetTopupPendingForCompletionAsync(row.OrderId, row.TransactionId, ct);
        if (state is null || !V1TopupRecoveryRules.MatchesOriginal(row, state))
            return new(false, false, "changed", "Payment details changed. Refresh the list before retrying.");

        await audit.WriteOutcomeAsync("TopupRecoveryStarted", "Topup", row.OrderId, true,
            "Administrator requested verification of an existing V1 top-up.", null,
            new { row.Id, row.StudentId, row.OrderId, row.Amount }, ct);
        try
        {
            var result = await completion.CompleteAsync(new TopupCompleteRequest
            {
                StudentId = row.StudentId,
                OrderId = row.OrderId,
                TransactionId = row.TransactionId,
                UseNativeCredentials = false
            }, ct);
            return new(result.IsSuccess, false, result.Status,
                result.IsSuccess
                    ? (result.IsAlreadyProcessed ? "Top-up was already completed. No additional credit was applied." : "Top-up credited successfully.")
                    : string.IsNullOrWhiteSpace(result.Message) ? "Payment is not yet verified. No credit was applied." : result.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Admin V1 top-up recovery outcome uncertain. TransactionPkId={TransactionPkId}", id);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await audit.WriteOutcomeAsync("TopupRecoveryUnconfirmed", "Topup", row.OrderId, false,
                "Recovery did not return a confirmed outcome; check the original payment before retrying.",
                null, new { row.Id }, timeout.Token);
            return new(false, true, "unconfirmed",
                "The result could not be confirmed. The original reference is retained. Refresh and check before retrying.");
        }
        finally { await logs.FlushAsync(); }
    }
}
