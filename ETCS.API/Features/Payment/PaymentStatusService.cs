using ETCS.Shared.Application.Payment;
using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Enumeration;
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
            _completionCancellation.CaptureToken(cancellationToken));

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
            _completionCancellation.CaptureToken(cancellationToken));

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
