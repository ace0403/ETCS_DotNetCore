using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Application.Payment;

public interface IPendingPaymentReconcileService
{
    Task ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed class PendingPaymentReconcileService : IPendingPaymentReconcileService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITopupPaymentCompleteService _topupPaymentCompleteService;
    private readonly IOrderPaymentCompleteService _orderPaymentCompleteService;
    private readonly PendingPaymentReconcileOptions _options;
    private readonly ILogger<PendingPaymentReconcileService> _logger;

    public PendingPaymentReconcileService(
        ITransactionRepository transactionRepository,
        ITopupPaymentCompleteService topupPaymentCompleteService,
        IOrderPaymentCompleteService orderPaymentCompleteService,
        IOptions<PendingPaymentReconcileOptions> options,
        ILogger<PendingPaymentReconcileService> logger)
    {
        _transactionRepository = transactionRepository;
        _topupPaymentCompleteService = topupPaymentCompleteService;
        _orderPaymentCompleteService = orderPaymentCompleteService;
        _options = options.Value;
        _logger = logger;
    }

    public async Task ProcessBatchAsync(CancellationToken cancellationToken)
    {
        var lookbackHours = Math.Max(1, _options.LookbackHours);
        var maxAttempts = Math.Max(1, _options.MaxAttempts);
        var batchSize = Math.Max(1, _options.BatchSize);

        var pending = await _transactionRepository.ListPendingForReconcileAsync(
            lookbackHours,
            maxAttempts,
            batchSize,
            cancellationToken);

        if (pending.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "Pending payment reconcile: processing {Count} candidate(s) (lookback {LookbackHours}h, maxAttempts {MaxAttempts}).",
            pending.Count,
            lookbackHours,
            maxAttempts);

        foreach (var item in pending)
        {
            try
            {
                await ReconcileOneAsync(item, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Pending payment reconcile failed for {Kind} OrderId={OrderId} TransactionPkId={TransactionPkId}.",
                    item.Kind,
                    item.OrderId,
                    item.TransactionPkId);

                try
                {
                    await _transactionRepository.BumpReconcileAttemptAsync(item.TransactionPkId, cancellationToken);
                }
                catch (Exception bumpEx)
                {
                    _logger.LogWarning(
                        bumpEx,
                        "Failed to bump reconcile attempt for TransactionPkId={TransactionPkId}.",
                        item.TransactionPkId);
                }
            }
        }
    }

    private async Task ReconcileOneAsync(PendingPaymentReconcileItem item, CancellationToken cancellationToken)
    {
        if (item.StatusId is not ((int)TransactionStatusEnum.Initiated or (int)TransactionStatusEnum.Pending))
        {
            _logger.LogInformation(
                "Skipping {Kind} OrderId={OrderId}: StatusId={StatusId} is not pending/initiated.",
                item.Kind,
                item.OrderId,
                item.StatusId);
            return;
        }

        if (string.IsNullOrWhiteSpace(item.OrderId) || string.IsNullOrWhiteSpace(item.GatewayTransactionId))
        {
            await _transactionRepository.BumpReconcileAttemptAsync(item.TransactionPkId, cancellationToken);
            return;
        }

        if (item.Kind == PendingPaymentReconcileKind.Topup)
        {
            var result = await _topupPaymentCompleteService.CompleteAsync(
                new TopupCompleteRequest
                {
                    StudentId = item.StudentId,
                    OrderId = item.OrderId,
                    TransactionId = item.GatewayTransactionId
                },
                cancellationToken);

            if (result.IsAlreadyProcessed)
            {
                _logger.LogDebug(
                    "Top-up OrderId={OrderId} already completed; skipping attempt bump.",
                    item.OrderId);
                return;
            }

            if (result.IsSuccess && !result.IsPending)
            {
                _logger.LogInformation("Reconciled top-up OrderId={OrderId} successfully.", item.OrderId);
                return;
            }

            await _transactionRepository.BumpReconcileAttemptAsync(item.TransactionPkId, cancellationToken);
            _logger.LogInformation(
                "Top-up OrderId={OrderId} still pending/failed after reconcile (Attempt {Attempt}/{Max}). Message={Message}",
                item.OrderId,
                item.ReconcileAttemptCount + 1,
                _options.MaxAttempts,
                result.Message);
            return;
        }

        var orderResult = await _orderPaymentCompleteService.CompleteAsync(
            new OrderCompleteRequest
            {
                StudentId = item.StudentId,
                GuardianId = item.GuardianId,
                OrderId = item.OrderId,
                TransactionId = item.GatewayTransactionId
            },
            cancellationToken);

        if (orderResult.IsAlreadyProcessed)
        {
            _logger.LogDebug(
                "Order OrderId={OrderId} already completed; skipping attempt bump.",
                item.OrderId);
            return;
        }

        if (orderResult.IsSuccess && !orderResult.IsPending)
        {
            _logger.LogInformation("Reconciled order OrderId={OrderId} successfully.", item.OrderId);
            return;
        }

        await _transactionRepository.BumpReconcileAttemptAsync(item.TransactionPkId, cancellationToken);
        _logger.LogInformation(
            "Order OrderId={OrderId} still pending/failed after reconcile (Attempt {Attempt}/{Max}). Message={Message}",
            item.OrderId,
            item.ReconcileAttemptCount + 1,
            _options.MaxAttempts,
            orderResult.Message);
    }
}
