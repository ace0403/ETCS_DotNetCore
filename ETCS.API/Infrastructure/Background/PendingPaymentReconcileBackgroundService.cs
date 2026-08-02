using ETCS.Shared.Application.Payment;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;

namespace ETCS.API.Infrastructure.Background;

public sealed class PendingPaymentReconcileBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly PendingPaymentReconcileOptions _options;
    private readonly ILogger<PendingPaymentReconcileBackgroundService> _logger;

    public PendingPaymentReconcileBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<PendingPaymentReconcileOptions> options,
        ILogger<PendingPaymentReconcileBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalMinutes = Math.Max(1, _options.IntervalMinutes);
        var delay = TimeSpan.FromMinutes(intervalMinutes);

        _logger.LogInformation(
            "Pending payment reconcile worker started. Enabled={Enabled}, IntervalMinutes={IntervalMinutes}.",
            _options.Enabled,
            intervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (_options.Enabled)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<IPendingPaymentReconcileService>();
                    await service.ProcessBatchAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Pending payment reconcile batch failed.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
