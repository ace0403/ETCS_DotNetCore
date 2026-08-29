using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using System.Globalization;
using System.Text.Json;

namespace ETCS.Shared.Application.Topup;

public sealed class TopupInitiateService : ITopupInitiateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;

    public TopupInitiateService(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IStudentOrderTypeAccessService orderTypeAccess)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _orderTypeAccess = orderTypeAccess;
    }

    public async Task<TopupInitiateResponse> InitiateAsync(
        TopupInitiateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GuardianId <= 0)
        {
            return Fail("Guardian is required.");
        }

        if (string.IsNullOrWhiteSpace(request.StudentId))
        {
            return Fail("Student is required.");
        }

        if (!int.TryParse(request.StudentId.Trim(), out var studentPk) || studentPk <= 0)
        {
            return Fail("Student is invalid.");
        }

        if (request.Amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        var parentDetails = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.Trim(),
            cancellationToken);
        if (parentDetails is null)
        {
            return Fail("Unable to resolve guardian details for this student.");
        }

        if (parentDetails.GuardianId != request.GuardianId)
        {
            return Fail("You do not have access to top up this student.");
        }

        if (!await _orderTypeAccess.IsAllowedAsync(studentPk, (int)TransactionTypeEnum.Topup, cancellationToken))
        {
            return Fail(_orderTypeAccess.GetDeniedMessage((int)TransactionTypeEnum.Topup));
        }

        var minimumTopup = await _studentRepository.GetStudentMinimumTopupAsync(studentPk, cancellationToken);
        if (!TopupAmountRules.MeetsMinimum(request.Amount, minimumTopup))
        {
            var minimum = minimumTopup ?? 0m;
            return new TopupInitiateResponse
            {
                IsSuccess = false,
                Message = $"Minimum top-up amount for this student is {minimum.ToString("F2", CultureInfo.InvariantCulture)}.",
                MinimumTopupAmount = minimum
            };
        }

        var orderId = OrderIdGenerator.GenerateForStudent(request.StudentId);
        var topupTransactionPkId = await _transactionRepository.CreateTopupPendingTransactionAsync(
            new TopupTransactionCreateRequest
            {
                GuardianId = parentDetails.GuardianId,
                StudentId = studentPk,
                Amount = request.Amount,
                Remarks = orderId,
                StatusId = (int)TransactionStatusEnum.Pending,
                CreatedBy = parentDetails.GuardianId
            },
            cancellationToken);

        try
        {
            var paymentRequest = new StudentTopupPaymentRequest(request.StudentId.Trim(), request.Amount);
            var result = await _paymentGatewayRepository.CreateTopupSessionAsync(
                paymentRequest,
                orderId,
                cancellationToken,
                request.ReturnUrl);

            var pgResponse = JsonSerializer.Serialize(result, JsonOptions);
            _paymentBackgroundQueue.EnqueuePaymentLog(orderId, pgResponse ?? string.Empty);

            if (!result.IsSuccess)
            {
                await MarkTopupFailedAsync(
                    topupTransactionPkId,
                    parentDetails.GuardianId,
                    result.TransactionId,
                    string.IsNullOrWhiteSpace(result.Message) ? "Payment session creation failed." : result.Message);

                return Fail(string.IsNullOrWhiteSpace(result.Message)
                    ? "Unable to create payment session."
                    : result.Message);
            }

            // Keep OrderId in Remarks — completion looks up by deep-link orderid.
            await _transactionRepository.UpdateTopupTransactionStatusAsync(
                new TopupTransactionUpdateRequest
                {
                    TransactionPkId = topupTransactionPkId,
                    GatewayTransactionId = result.TransactionId,
                    StatusId = (int)TransactionStatusEnum.Initiated,
                    IsTransactionCompleted = false,
                    Remarks = orderId,
                    UpdatedBy = parentDetails.GuardianId
                },
                cancellationToken);

            var requestObj = new
            {
                GUID = orderId,
                TransactionId = result.TransactionId,
                GrdId = parentDetails.GuardianId,
                CustomerId = parentDetails.CustomerId,
                GuardianEmail = parentDetails.Email,
                Amount = request.Amount.ToString(CultureInfo.InvariantCulture),
                TransactionType = "topup"
            };

            await _transactionRepository.InsertPendingTransactionAsync(
                new PendingTransactionRequest
                {
                    CustomerID = parentDetails.CustomerId,
                    Creby = parentDetails.Email,
                    Amount = request.Amount.ToString(CultureInfo.InvariantCulture),
                    Loaded = "0",
                    TransDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                    Remarks = orderId,
                    Mode = "O",
                    BankName = "ETISALAT",
                    PaymentDetails = result.TransactionId,
                    Billdate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                    RequestObject = JsonSerializer.Serialize(requestObj)
                },
                cancellationToken);

            return new TopupInitiateResponse
            {
                IsSuccess = true,
                Message = "Payment session created.",
                OrderId = orderId,
                TransactionId = result.TransactionId,
                RedirectUrl = result.RedirectUrl,
                MinimumTopupAmount = minimumTopup ?? 0m
            };
        }
        catch (Exception ex)
        {
            await MarkTopupFailedAsync(
                topupTransactionPkId,
                parentDetails.GuardianId,
                gatewayTransactionId: string.Empty,
                remarks: ex is OperationCanceledException
                    ? "Payment session request timed out or was cancelled."
                    : "Payment session creation failed unexpectedly.");

            if (ex is OperationCanceledException)
            {
                return Fail("Payment gateway request timed out or was cancelled. Please retry.");
            }

            throw;
        }
    }

    private async Task MarkTopupFailedAsync(
        int topupTransactionPkId,
        int guardianId,
        string? gatewayTransactionId,
        string remarks)
    {
        // Use None so a cancelled request still marks the MealDB row Failed.
        await _transactionRepository.UpdateTopupTransactionStatusAsync(
            new TopupTransactionUpdateRequest
            {
                TransactionPkId = topupTransactionPkId,
                GatewayTransactionId = string.IsNullOrWhiteSpace(gatewayTransactionId) ? string.Empty : gatewayTransactionId,
                StatusId = (int)TransactionStatusEnum.Failed,
                IsTransactionCompleted = false,
                Remarks = remarks,
                UpdatedBy = guardianId
            },
            CancellationToken.None);
    }

    private static TopupInitiateResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
