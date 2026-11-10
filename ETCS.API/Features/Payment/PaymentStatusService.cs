using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Payment;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.API.Features.Payment;

public interface IPaymentStatusService
{
    Task<PaymentStatusResponse> GetStatusAsync(
        string orderId,
        string transactionId,
        int studentId,
        string paymentType,
        CancellationToken cancellationToken);
}

public sealed class PaymentStatusService : IPaymentStatusService
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly PaymentCompletionCancellation _completionCancellation;

    public PaymentStatusService(
        IMealOrderRepository mealOrderRepository,
        ITransactionRepository transactionRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        PaymentCompletionCancellation completionCancellation)
    {
        _mealOrderRepository = mealOrderRepository;
        _transactionRepository = transactionRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _completionCancellation = completionCancellation;
    }

    public async Task<PaymentStatusResponse> GetStatusAsync(
        string orderId,
        string transactionId,
        int studentId,
        string paymentType,
        CancellationToken cancellationToken)
    {
        var normalizedType = string.IsNullOrWhiteSpace(paymentType)
            ? "order"
            : paymentType.Trim().ToLowerInvariant();

        if (normalizedType is "topup")
        {
            return await GetTopupStatusAsync(orderId, transactionId, studentId, cancellationToken);
        }

        return await GetOrderStatusAsync(orderId, transactionId, studentId, cancellationToken);
    }

    private async Task<PaymentStatusResponse> GetOrderStatusAsync(
        string orderId,
        string transactionId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var paymentState = await _mealOrderRepository.GetPaymentStateAsync(orderId, cancellationToken);
        if (paymentState is null)
        {
            return new PaymentStatusResponse
            {
                IsSuccess = false,
                Message = "Order not found.",
                OrderId = orderId,
                TransactionId = transactionId
            };
        }

        if (paymentState.IsPaid)
        {
            return new PaymentStatusResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "Order payment already completed.",
                OrderId = orderId,
                TransactionId = transactionId,
                Status = "completed"
            };
        }

        var captureResult = await _paymentGatewayRepository.CapturePaymentAsync(
            new PaymentCaptureRequest(transactionId, orderId, studentId),
            cancellationToken);

        return MapCaptureResult(captureResult, orderId, transactionId);
    }

    private async Task<PaymentStatusResponse> GetTopupStatusAsync(
        string orderId,
        string transactionId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var topupState = await _transactionRepository.GetTopupPendingByOrderIdAsync(
            orderId,
            transactionId,
            cancellationToken);

        if (topupState is { IsV1AppTopup: true } &&
            (!string.Equals(topupState.OrderId, orderId, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(topupState.GatewayTransactionId, transactionId, StringComparison.Ordinal)
                || topupState.StudentId != studentId))
            return new PaymentStatusResponse { Message = "Payment references do not match the top-up." };

        if (topupState is { HasUncreditedV1Capture: true })
        {
            return new PaymentStatusResponse
            {
                IsSuccess = true,
                Message = "Payment captured. Complete the payment to apply the top-up credit.",
                OrderId = orderId,
                TransactionId = transactionId,
                Status = "captured"
            };
        }

        if (topupState is { IsTransactionCompleted: true })
        {
            return new PaymentStatusResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "Top-up already completed.",
                OrderId = orderId,
                TransactionId = transactionId,
                Status = "completed"
            };
        }

        var captureResult = await _paymentGatewayRepository.CapturePaymentAsync(
            new PaymentCaptureRequest(transactionId, orderId, studentId),
            cancellationToken);

        if (topupState is { IsV1AppTopup: true } && captureResult.IsSuccess && !captureResult.IsPending
            && string.Equals(captureResult.TransactionId, transactionId, StringComparison.Ordinal))
        {
            // Gateway finalization can finish even if the app closes before posting topup/update.
            using var dbTimeout = _completionCancellation.CreateDbTimeoutSource();
            await _transactionRepository.RecordV1TopupCaptureAsync(topupState.TransactionPkId, dbTimeout.Token);
        }

        return MapCaptureResult(captureResult, orderId, transactionId);
    }

    private static PaymentStatusResponse MapCaptureResult(
        PaymentCaptureResult captureResult,
        string orderId,
        string transactionId)
    {
        if (captureResult.IsPending)
        {
            return new PaymentStatusResponse
            {
                IsPending = true,
                Message = string.IsNullOrWhiteSpace(captureResult.Message)
                    ? "Payment is still processing."
                    : captureResult.Message,
                OrderId = orderId,
                TransactionId = string.IsNullOrWhiteSpace(captureResult.TransactionId)
                    ? transactionId
                    : captureResult.TransactionId,
                Status = captureResult.Status
            };
        }

        if (!captureResult.IsSuccess)
        {
            return new PaymentStatusResponse
            {
                IsSuccess = false,
                Message = string.IsNullOrWhiteSpace(captureResult.Message)
                    ? "Payment capture failed."
                    : captureResult.Message,
                OrderId = orderId,
                TransactionId = transactionId,
                Status = captureResult.Status
            };
        }

        return new PaymentStatusResponse
        {
            IsSuccess = true,
            Message = "Payment captured by gateway. Complete the payment to finalize.",
            OrderId = orderId,
            TransactionId = string.IsNullOrWhiteSpace(captureResult.TransactionId)
                ? transactionId
                : captureResult.TransactionId,
            Status = captureResult.Status
        };
    }
}
