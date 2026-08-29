using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Application.Payment;

/// <summary>
/// Builds timeout-linked cancellation sources for payment capture and DB finalize.
/// Callers must keep the returned <see cref="CancellationTokenSource"/> alive (use <c>using</c>)
/// for the duration of the async work — returning only <see cref="CancellationToken"/> from a
/// local CTS allows GC to dispose it early and cancel the operation.
/// </summary>
public sealed class PaymentCompletionCancellation
{
    private readonly PaymentGatewayOptions _options;

    public PaymentCompletionCancellation(IOptions<PaymentGatewayOptions> options)
    {
        _options = options.Value;
    }

    public CancellationTokenSource CreateCaptureTimeoutSource(CancellationToken requestToken)
    {
        var seconds = _options.CaptureTimeoutSeconds <= 0 ? 90 : _options.CaptureTimeoutSeconds;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        cts.CancelAfter(TimeSpan.FromSeconds(seconds));
        return cts;
    }

    public CancellationTokenSource CreateDbTimeoutSource()
    {
        var seconds = _options.CompletionDbTimeoutSeconds <= 0 ? 180 : _options.CompletionDbTimeoutSeconds;
        return new CancellationTokenSource(TimeSpan.FromSeconds(seconds));
    }
}
