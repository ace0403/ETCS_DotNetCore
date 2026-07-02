using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Application.Payment;

public sealed class PaymentCompletionCancellation
{
    private readonly PaymentGatewayOptions _options;

    public PaymentCompletionCancellation(IOptions<PaymentGatewayOptions> options)
    {
        _options = options.Value;
    }

    public CancellationToken CaptureToken(CancellationToken requestToken)
    {
        var seconds = _options.CaptureTimeoutSeconds <= 0 ? 90 : _options.CaptureTimeoutSeconds;
        var cts = CancellationTokenSource.CreateLinkedTokenSource(requestToken);
        cts.CancelAfter(TimeSpan.FromSeconds(seconds));
        return cts.Token;
    }

    public CancellationToken DbToken()
    {
        var seconds = _options.CompletionDbTimeoutSeconds <= 0 ? 180 : _options.CompletionDbTimeoutSeconds;
        return new CancellationTokenSource(TimeSpan.FromSeconds(seconds)).Token;
    }
}
