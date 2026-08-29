using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
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
    private readonly IGuardianInAppNotificationService _inAppNotificationService;
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
        IGuardianInAppNotificationService inAppNotificationService,
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
        _inAppNotificationService = inAppNotificationService;
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

        var alreadyMarkedSuccess = paymentState.IsPaid
            || paymentState.IsTransactionCompleted
            || paymentState.TransactionStatusId == (int)TransactionStatusEnum.Success;

        // Resume path: Success/IsPaid without AccessLogId — finish ledger attach without re-capturing.
        if (alreadyMarkedSuccess && !paymentState.AccessLogId.HasValue)
        {
            return await ResumeAccessLogAttachAsync(request, paymentState, cancellationToken);
        }

        if (paymentState.TransactionStatusId is not ((int)TransactionStatusEnum.Initiated or (int)TransactionStatusEnum.Pending))
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                Message = $"Order payment is not pending (StatusId={paymentState.TransactionStatusId}).",
                OrderId = request.OrderId,
                GatewayTransactionId = request.TransactionId
            };
        }

        var captureResult = await _paymentGatewayRepository.CapturePaymentAsync(
            new PaymentCaptureRequest(request.TransactionId, request.OrderId, request.StudentId),
            cancellationToken);

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

        using var dbTimeout = _completionCancellation.CreateDbTimeoutSource();
        var dbToken = dbTimeout.Token;

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

        var gatewayTransactionId = string.IsNullOrWhiteSpace(captureResult.TransactionId)
            ? request.TransactionId
            : captureResult.TransactionId;

        await _mealOrderRepository.MarkPaymentCompletedAsync(
            request.OrderId,
            gatewayTransactionId,
            (int)TransactionStatusEnum.Success,
            (int)TransactionStatusEnum.Success,
            dbToken);

        var accessLogId = await EnsureAccessLogAttachedAsync(
            request.OrderId,
            guardianDetail.CustomerId,
            gatewayTransactionId,
            paymentState.Total,
            paymentState.OrderTypeId,
            dbToken);

        var paymentDetails = gatewayTransactionId;

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

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(request.StudentId, dbToken);
        var mealLabel = paymentState.OrderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => "A La Carte",
            (int)TransactionTypeEnum.MealOrder => "Meal Plan",
            (int)TransactionTypeEnum.POS => "POS",
            _ => "Order"
        };

        var studentName = string.Empty;
        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(request.GuardianId, dbToken);
        var matched = students.FirstOrDefault(s => s.UserId == request.StudentId);
        if (matched is not null && !string.IsNullOrWhiteSpace(matched.Name))
        {
            studentName = matched.Name.Trim();
        }

        await _inAppNotificationService.CreateOrderSuccessAsync(
            request.StudentId,
            request.GuardianId,
            studentName,
            request.OrderId,
            mealLabel,
            schoolId,
            dbToken);

        return new OrderCompleteResponse
        {
            IsSuccess = true,
            Message = "Order payment completed successfully.",
            OrderId = request.OrderId,
            GatewayTransactionId = gatewayTransactionId,
            AccessLogId = accessLogId
        };
    }

    private async Task<OrderCompleteResponse> ResumeAccessLogAttachAsync(
        OrderCompleteRequest request,
        MealOrderPaymentState paymentState,
        CancellationToken cancellationToken)
    {
        using var dbTimeout = _completionCancellation.CreateDbTimeoutSource();
        var dbToken = dbTimeout.Token;

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
                GatewayTransactionId = request.TransactionId ?? string.Empty
            };
        }

        var gatewayTransactionId = (request.TransactionId ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            gatewayTransactionId = await _mealOrderRepository.GetGatewayTransactionIdByOrderIdAsync(
                request.OrderId,
                dbToken) ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return new OrderCompleteResponse
            {
                IsSuccess = false,
                Message = "Unable to resolve gateway transaction id for AccessLog attach.",
                OrderId = request.OrderId,
                GatewayTransactionId = request.TransactionId ?? string.Empty
            };
        }

        var accessLogId = await EnsureAccessLogAttachedAsync(
            request.OrderId,
            guardianDetail.CustomerId,
            gatewayTransactionId,
            paymentState.Total,
            paymentState.OrderTypeId,
            dbToken);

        return new OrderCompleteResponse
        {
            IsSuccess = true,
            IsAlreadyProcessed = true,
            Message = "Order payment already completed; AccessLog linked.",
            OrderId = request.OrderId,
            GatewayTransactionId = gatewayTransactionId,
            AccessLogId = accessLogId
        };
    }

    private async Task<long> EnsureAccessLogAttachedAsync(
        string orderId,
        string customerId,
        string gatewayTransactionId,
        decimal total,
        int orderTypeId,
        CancellationToken cancellationToken)
    {
        var existing = await _mainOrderRepository.FindAccessLogIdByGatewayTransactionAsync(
            customerId,
            gatewayTransactionId,
            cancellationToken);

        long accessLogId;
        if (existing is > 0)
        {
            accessLogId = existing.Value;
        }
        else
        {
            var (accessLogTypeId, orderDescription) = OrderAccessLogResolver.Resolve(orderTypeId);
            accessLogId = await _mainOrderRepository.ApplySuccessfulOrderAsync(
                customerId,
                orderId,
                gatewayTransactionId,
                total,
                orderDescription,
                (short)accessLogTypeId,
                _orderFlowOptions.AccessLogDescription,
                "777",
                "240",
                cancellationToken);
        }

        await _mealOrderRepository.AttachAccessLogIdAsync(orderId, accessLogId, cancellationToken);
        return accessLogId;
    }
}
