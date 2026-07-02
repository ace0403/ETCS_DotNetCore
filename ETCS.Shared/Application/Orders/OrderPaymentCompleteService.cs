using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Text.Json;

namespace ETCS.Shared.Application.Orders;

public sealed class OrderPaymentCompleteService : IOrderPaymentCompleteService
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IMainOrderRepository _mainOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly PaymentCompletionCancellation _completionCancellation;
    private readonly OrderFlowOptions _orderFlowOptions;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrderPaymentCompleteService(
        IMealOrderRepository mealOrderRepository,
        IMainOrderRepository mainOrderRepository,
        IStudentRepository studentRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        ITransactionRepository transactionRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IGuardianEmailNotificationService emailNotificationService,
        PaymentCompletionCancellation completionCancellation,
        IOptions<OrderFlowOptions> orderFlowOptions)
    {
        _mealOrderRepository = mealOrderRepository;
        _mainOrderRepository = mainOrderRepository;
        _studentRepository = studentRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _transactionRepository = transactionRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _emailNotificationService = emailNotificationService;
        _completionCancellation = completionCancellation;
        _orderFlowOptions = orderFlowOptions.Value;
    }

    public async Task<OrderCompleteResponse> CompleteAsync(OrderCompleteRequest request, CancellationToken cancellationToken)
    {
        var paymentState = await _mealOrderRepository.GetPaymentStateForCompletionAsync(request.OrderId, cancellationToken);
        if (paymentState is null)
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                Message = "Order not found.",
                OrderId = request.OrderId,
                GatewayTransactionId = request.TransactionId
            };
        }

        if (paymentState.IsPaid && paymentState.AccessLogId.HasValue)
        {
            return new OrderCompleteResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "Order payment already completed.",
                OrderId = request.OrderId,
                GatewayTransactionId = request.TransactionId,
                AccessLogId = paymentState.AccessLogId.Value
            };
        }

        var captureResult = await _paymentGatewayRepository.CapturePaymentAsync(
            new PaymentCaptureRequest(request.TransactionId, request.OrderId, request.StudentId),
            _completionCancellation.CaptureToken(cancellationToken));

        var pgResponse = JsonSerializer.Serialize(captureResult, JsonOptions);
        _paymentBackgroundQueue.EnqueuePaymentLog(request.OrderId, pgResponse ?? string.Empty);

        if (captureResult.IsPending)
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                IsPending = true,
                Message = string.IsNullOrWhiteSpace(captureResult.Message)
                    ? "Payment is still processing."
                    : captureResult.Message,
                OrderId = request.OrderId,
                GatewayTransactionId = string.IsNullOrWhiteSpace(captureResult.TransactionId)
                    ? request.TransactionId
                    : captureResult.TransactionId
            };
        }

        if (!captureResult.IsSuccess)
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                Message = string.IsNullOrWhiteSpace(captureResult.Message)
                    ? "Payment capture failed."
                    : captureResult.Message,
                OrderId = request.OrderId,
                GatewayTransactionId = request.TransactionId
            };
        }

        var dbToken = _completionCancellation.DbToken();

        await _mealOrderRepository.MarkPaymentCompletedAsync(
            request.OrderId,
            captureResult.TransactionId,
            (int)TransactionStatusEnum.Success,
            (int)TransactionStatusEnum.Success,
            dbToken);

        var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.ToString(CultureInfo.InvariantCulture),
            dbToken);
        if (guardianDetail is null || string.IsNullOrWhiteSpace(guardianDetail.CustomerId))
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                Message = "Unable to resolve customer profile for this student.",
                OrderId = request.OrderId,
                GatewayTransactionId = captureResult.TransactionId
            };
        }

        var (accessLogTypeId, orderDescription) = OrderAccessLogResolver.Resolve(paymentState.OrderTypeId);

        var accessLogId = await _mainOrderRepository.ApplySuccessfulOrderAsync(
            guardianDetail.CustomerId,
            request.OrderId,
            captureResult.TransactionId,
            paymentState.Total,
            orderDescription,
            (short)accessLogTypeId,
            _orderFlowOptions.AccessLogDescription,
            "777",
            "240",
            dbToken);

        await _mealOrderRepository.AttachAccessLogIdAsync(request.OrderId, accessLogId, dbToken);

        var paymentDetails = string.IsNullOrWhiteSpace(captureResult.TransactionId)
            ? request.TransactionId
            : captureResult.TransactionId;

        await _transactionRepository.UpdatePendingTransactionAsync(
            new UpdatePendingTransactionRequest
            {
                CustomerID = guardianDetail.CustomerId,
                Loaded = "1",
                Creby = guardianDetail.Email,
                PaymentDetails = paymentDetails,
                Remarks = request.OrderId
            },
            dbToken);

        await _emailNotificationService.QueueOrderSuccessAsync(
            request.StudentId,
            request.GuardianId,
            guardianDetail.Email,
            guardianDetail.GuardianName,
            paymentState.OrderTypeId,
            request.OrderId,
            paymentDetails,
            paymentState.Total,
            dbToken);

        return new OrderCompleteResponse
        {
            IsSuccess = true,
            Message = "Order payment completed successfully.",
            OrderId = request.OrderId,
            GatewayTransactionId = captureResult.TransactionId,
            AccessLogId = accessLogId
        };
    }
}
