using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Notifications;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
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
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly IGuardianInAppNotificationService _inAppNotificationService;
    private readonly PaymentCompletionCancellation _completionCancellation;

    public TopupPaymentCompleteService(
        ITransactionRepository transactionRepository,
        IStudentRepository studentRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IGuardianEmailNotificationService emailNotificationService,
        IGuardianInAppNotificationService inAppNotificationService,
        PaymentCompletionCancellation completionCancellation)
    {
        _transactionRepository = transactionRepository;
        _studentRepository = studentRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _emailNotificationService = emailNotificationService;
        _inAppNotificationService = inAppNotificationService;
        _completionCancellation = completionCancellation;
    }

    public async Task<TopupCompleteResponse> CompleteAsync(
        TopupCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return Fail("TransactionId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Fail("OrderId is required.");
        }

        var topupState = await _transactionRepository.GetTopupPendingForCompletionAsync(
            request.OrderId,
            request.TransactionId,
            cancellationToken);

        if (topupState is { IsTransactionCompleted: true }
            || topupState is { StatusId: (int)TransactionStatusEnum.Success })
        {
            return new TopupCompleteResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "Top-up already completed.",
                OrderId = request.OrderId,
                TransactionId = request.TransactionId,
                Amount = topupState.Amount,
                Status = "completed"
            };
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
                TransactionId = request.TransactionId,
                Amount = topupState.Amount,
                Status = "skipped"
            };
        }

        var captureRequest = new PaymentCaptureRequest(
            request.TransactionId,
            request.OrderId,
            request.StudentId);

        var result = await _paymentGatewayRepository.CapturePaymentAsync(
            captureRequest,
            _completionCancellation.CaptureToken(cancellationToken));

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
            ? request.TransactionId
            : result.TransactionId;

        if (paymentCompleted)
        {
            var dbToken = _completionCancellation.DbToken();

            var parentDetails = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                request.StudentId.ToString(CultureInfo.InvariantCulture),
                dbToken);

            if (topupState is not null)
            {
                await _transactionRepository.UpdateTopupTransactionStatusAsync(
                    new TopupTransactionUpdateRequest
                    {
                        TransactionPkId = topupState.TransactionPkId,
                        GatewayTransactionId = gatewayTransactionId,
                        StatusId = (int)TransactionStatusEnum.Success,
                        IsTransactionCompleted = true,
                        Remarks = string.IsNullOrWhiteSpace(result.Message) ? "Topup completed." : result.Message,
                        UpdatedBy = parentDetails?.GuardianId
                    },
                    dbToken);
            }

            await _transactionRepository.UpdatePendingAndTopupTransactionAsync(
                new UpdatePendingTransactionRequest
                {
                    CustomerID = parentDetails?.CustomerId ?? string.Empty,
                    Loaded = "1",
                    Creby = parentDetails?.Email ?? string.Empty,
                    PaymentDetails = gatewayTransactionId,
                    Remarks = request.OrderId
                },
                new UpdateTopupTransactionRequest
                {
                    CustomerID = parentDetails?.CustomerId ?? string.Empty,
                    Remarks = request.OrderId
                },
                dbToken);

            var topupAmount = topupState?.Amount ?? 0m;
            if (parentDetails is not null)
            {
                await _emailNotificationService.QueueTopupSuccessAsync(
                    request.StudentId,
                    parentDetails.GuardianId,
                    parentDetails.Email,
                    parentDetails.GuardianName,
                    request.OrderId,
                    gatewayTransactionId,
                    topupAmount,
                    dbToken);

                var schoolId = await _studentRepository.GetStudentSchoolIdAsync(request.StudentId, dbToken);
                await _inAppNotificationService.CreateTopupSuccessAsync(
                    request.StudentId,
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

    private static TopupCompleteResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
