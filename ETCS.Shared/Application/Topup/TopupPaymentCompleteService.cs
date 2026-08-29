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
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

namespace ETCS.Shared.Application.Topup;

public sealed class TopupPaymentCompleteService : ITopupPaymentCompleteService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITransactionRepository _transactionRepository;
    private readonly IMainOrderRepository _mainOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly IGuardianInAppNotificationService _inAppNotificationService;
    private readonly PaymentCompletionCancellation _completionCancellation;
    private readonly ILogger<TopupPaymentCompleteService> _logger;

    public TopupPaymentCompleteService(
        ITransactionRepository transactionRepository,
        IMainOrderRepository mainOrderRepository,
        IStudentRepository studentRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IGuardianEmailNotificationService emailNotificationService,
        IGuardianInAppNotificationService inAppNotificationService,
        PaymentCompletionCancellation completionCancellation,
        ILogger<TopupPaymentCompleteService> logger)
    {
        _transactionRepository = transactionRepository;
        _mainOrderRepository = mainOrderRepository;
        _studentRepository = studentRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _emailNotificationService = emailNotificationService;
        _inAppNotificationService = inAppNotificationService;
        _completionCancellation = completionCancellation;
        _logger = logger;
    }

    public async Task<TopupCompleteResponse> CompleteAsync(
        TopupCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Fail("OrderId is required.");
        }

        // Mobile deep link only returns orderid; resolve gateway TransactionId from MealDB when omitted.
        var topupState = await _transactionRepository.GetTopupPendingForCompletionAsync(
            request.OrderId,
            string.IsNullOrWhiteSpace(request.TransactionId) ? null : request.TransactionId,
            cancellationToken);

        var gatewayTransactionIdFromDb = topupState?.GatewayTransactionId?.Trim() ?? string.Empty;
        var transactionId = !string.IsNullOrWhiteSpace(request.TransactionId)
            ? request.TransactionId.Trim()
            : gatewayTransactionIdFromDb;

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return Fail("TransactionId is required.");
        }

        if (topupState is { IsTransactionCompleted: true }
            || topupState is { StatusId: (int)TransactionStatusEnum.Success })
        {
            if (topupState.AccessLogId is > 0)
            {
                return new TopupCompleteResponse
                {
                    IsSuccess = true,
                    IsAlreadyProcessed = true,
                    Message = "Top-up already completed.",
                    OrderId = request.OrderId,
                    TransactionId = transactionId,
                    Amount = topupState.Amount,
                    Status = "completed"
                };
            }

            // Resume AccessLog attach when Success but AccessLogId is missing.
            return await ResumeAccessLogAttachAsync(request, topupState, transactionId, cancellationToken);
        }

        // Do not capture/email unless MealDB StatusId is still Initiated or Pending.
        if (topupState is not null
            && topupState.StatusId is not ((int)TransactionStatusEnum.Initiated or (int)TransactionStatusEnum.Pending))
        {
            return new TopupCompleteResponse
            {
                IsSuccess = false,
                Message = $"Top-up is not pending (StatusId={topupState.StatusId}).",
                OrderId = request.OrderId,
                TransactionId = transactionId,
                Amount = topupState.Amount,
                Status = "skipped"
            };
        }

        var captureRequest = new PaymentCaptureRequest(
            transactionId,
            request.OrderId,
            request.StudentId > 0 ? request.StudentId : topupState?.StudentId ?? 0);

        var result = await _paymentGatewayRepository.CapturePaymentAsync(
            captureRequest,
            cancellationToken);

        if (!result.IsSuccess && !result.IsPending)
        {
            return Fail(string.IsNullOrWhiteSpace(result.Message)
                ? "Unable to capture payment status."
                : result.Message);
        }

        var pgResponse = JsonSerializer.Serialize(result, JsonOptions);
        _paymentBackgroundQueue.EnqueuePaymentLog(request.OrderId, pgResponse ?? string.Empty);

        var paymentCompleted = result.IsSuccess && !result.IsPending;
        var gatewayTransactionId = string.IsNullOrWhiteSpace(result.TransactionId)
            ? transactionId
            : result.TransactionId;

        if (paymentCompleted)
        {
            using var dbTimeout = _completionCancellation.CreateDbTimeoutSource();
            var dbToken = dbTimeout.Token;

            var studentId = request.StudentId > 0
                ? request.StudentId
                : topupState?.StudentId ?? 0;

            var parentDetails = studentId > 0
                ? await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                    studentId.ToString(CultureInfo.InvariantCulture),
                    dbToken)
                : null;

            if (parentDetails is null && topupState is { GuardianId: > 0 })
            {
                // Fallback: still finalize MealDB row even if guardian email lookup fails.
                parentDetails = new StudentGuardianBasicDetailDto(
                    topupState.GuardianId,
                    string.Empty,
                    string.Empty,
                    string.Empty);
            }

            if (topupState is not null)
            {
                // Preserve OrderId in Remarks so retries / status lookups still resolve.
                await _transactionRepository.UpdateTopupTransactionStatusAsync(
                    new TopupTransactionUpdateRequest
                    {
                        TransactionPkId = topupState.TransactionPkId,
                        GatewayTransactionId = gatewayTransactionId,
                        StatusId = (int)TransactionStatusEnum.Success,
                        IsTransactionCompleted = true,
                        Remarks = request.OrderId,
                        UpdatedBy = parentDetails?.GuardianId ?? topupState.GuardianId
                    },
                    dbToken);

                if (parentDetails is not null && !string.IsNullOrWhiteSpace(parentDetails.CustomerId))
                {
                    await EnsureTopupAccessLogAttachedAsync(
                        topupState.TransactionPkId,
                        parentDetails.CustomerId,
                        gatewayTransactionId,
                        topupState.Amount,
                        dbToken);
                }
            }

            var topupAmount = topupState?.Amount ?? 0m;
            if (parentDetails is not null && !string.IsNullOrWhiteSpace(parentDetails.CustomerId))
            {
                try
                {
                    if (topupAmount > 0)
                    {
                        await _transactionRepository.UpdatePrepaidBalanceAsync(
                            parentDetails.CustomerId,
                            topupAmount,
                            dbToken);
                    }

                    await _transactionRepository.UpdatePendingAndTopupTransactionAsync(
                        new UpdatePendingTransactionRequest
                        {
                            CustomerID = parentDetails.CustomerId,
                            Loaded = "1",
                            Creby = parentDetails.Email,
                            PaymentDetails = gatewayTransactionId,
                            Remarks = request.OrderId
                        },
                        new UpdateTopupTransactionRequest
                        {
                            CustomerID = parentDetails.CustomerId,
                            Remarks = request.OrderId
                        },
                        dbToken);
                }
                catch (Exception ex)
                {
                    // MealDB already marked Success — do not fail the client on wallet SP errors.
                    _logger.LogError(
                        ex,
                        "Wallet/pending update failed after successful capture. OrderId={OrderId}, GatewayTransactionId={GatewayTransactionId}",
                        request.OrderId,
                        gatewayTransactionId);
                }
            }

            if (parentDetails is not null && studentId > 0 && !string.IsNullOrWhiteSpace(parentDetails.Email))
            {
                await _emailNotificationService.QueueTopupSuccessAsync(
                    studentId,
                    parentDetails.GuardianId,
                    parentDetails.Email,
                    parentDetails.GuardianName,
                    request.OrderId,
                    gatewayTransactionId,
                    topupAmount,
                    dbToken);

                var schoolId = await _studentRepository.GetStudentSchoolIdAsync(studentId, dbToken);
                await _inAppNotificationService.CreateTopupSuccessAsync(
                    studentId,
                    parentDetails.GuardianId,
                    topupAmount,
                    request.OrderId,
                    schoolId,
                    dbToken);
            }
        }

        return new TopupCompleteResponse
        {
            IsSuccess = paymentCompleted,
            IsPending = result.IsPending,
            Message = string.IsNullOrWhiteSpace(result.Message)
                ? (paymentCompleted ? "Topup completed." : "Payment is still processing.")
                : result.Message,
            OrderId = request.OrderId,
            TransactionId = gatewayTransactionId,
            Amount = topupState?.Amount ?? 0m,
            Status = result.Status
        };
    }

    private async Task<TopupCompleteResponse> ResumeAccessLogAttachAsync(
        TopupCompleteRequest request,
        TopupPendingTransactionState topupState,
        string transactionId,
        CancellationToken cancellationToken)
    {
        using var dbTimeout = _completionCancellation.CreateDbTimeoutSource();
        var dbToken = dbTimeout.Token;

        var studentId = request.StudentId > 0 ? request.StudentId : topupState.StudentId;
        var parentDetails = studentId > 0
            ? await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                studentId.ToString(CultureInfo.InvariantCulture),
                dbToken)
            : null;

        if (parentDetails is null || string.IsNullOrWhiteSpace(parentDetails.CustomerId))
        {
            return new TopupCompleteResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "Top-up already completed (AccessLog attach skipped: no customer id).",
                OrderId = request.OrderId,
                TransactionId = transactionId,
                Amount = topupState.Amount,
                Status = "completed"
            };
        }

        await EnsureTopupAccessLogAttachedAsync(
            topupState.TransactionPkId,
            parentDetails.CustomerId,
            transactionId,
            topupState.Amount,
            dbToken);

        return new TopupCompleteResponse
        {
            IsSuccess = true,
            IsAlreadyProcessed = true,
            Message = "Top-up already completed; AccessLog linked.",
            OrderId = request.OrderId,
            TransactionId = transactionId,
            Amount = topupState.Amount,
            Status = "completed"
        };
    }

    private async Task EnsureTopupAccessLogAttachedAsync(
        int transactionPkId,
        string customerId,
        string gatewayTransactionId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (transactionPkId <= 0 || string.IsNullOrWhiteSpace(customerId) || string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return;
        }

        var linked = await _transactionRepository.GetAccessLogIdByTransactionPkAsync(transactionPkId, cancellationToken);
        if (linked is > 0)
        {
            return;
        }

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
            accessLogId = await _mainOrderRepository.InsertAccessLogAsync(
                customerId,
                amount,
                (short)AccessLogTypeEnum.Topup,
                "TOPUP RECHARGE",
                gatewayTransactionId,
                "777",
                "240",
                cancellationToken);
        }

        await _transactionRepository.AttachAccessLogIdByTransactionPkAsync(
            transactionPkId,
            accessLogId,
            cancellationToken);
    }

    private static TopupCompleteResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
