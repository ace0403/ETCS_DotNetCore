using ETCS.Shared.Application.Background;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.API.Infrastructure.Background;

public sealed class PaymentBackgroundService : BackgroundService
{
    private readonly PaymentBackgroundQueue _queue;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PaymentBackgroundService> _logger;

    public PaymentBackgroundService(
        PaymentBackgroundQueue queue,
        IServiceProvider serviceProvider,
        ILogger<PaymentBackgroundService> logger)
    {
        _queue = queue;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var log in _queue.PaymentLogReader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
                await repository.LogPaymentRequestAsync(log.TransactionId, log.Result, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Background payment log work item failed.");
            }
        }
    }
}
