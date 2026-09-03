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

namespace ETCS.Shared.Application.Payment;

public sealed class NativeTopupInitiateService : INativeTopupInitiateService
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

    public NativeTopupInitiateService(
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

    public async Task<NativeTopupInitiateResponse> InitiateAsync(
        NativeTopupInitiateRequest request,
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

        var paymentMethod = PaymentMethodEnumExtensions.ParseOrUnknown(request.PaymentMethod);
        if (paymentMethod is PaymentMethodEnum.Unknown)
        {
            paymentMethod = PaymentMethodEnum.Card;
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
            return new NativeTopupInitiateResponse
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
                CreatedBy = parentDetails.GuardianId,
                PaymentMethod = (int)paymentMethod
            },
            cancellationToken);

        try
        {
            if (paymentMethod is PaymentMethodEnum.ApplePay or PaymentMethodEnum.SamsungPay)
            {
                return await InitiateWalletTopupAsync(
                    request,
                    parentDetails,
                    orderId,
                    topupTransactionPkId,
                    studentPk,
                    paymentMethod,
                    minimumTopup,
                    cancellationToken);
            }

            return await InitiateCardTopupAsync(
                request,
                parentDetails,
                orderId,
                topupTransactionPkId,
                paymentMethod,
                minimumTopup,
                cancellationToken);
        }
        catch (Exception ex)
        {
            await MarkTopupFailedAsync(
                topupTransactionPkId,
                parentDetails.GuardianId,
                gatewayTransactionId: string.Empty,
                remarks: ex is OperationCanceledException
                    ? "Native payment session request timed out or was cancelled."
                    : "Native payment session creation failed unexpectedly.");

            if (ex is OperationCanceledException)
            {
                return Fail("Payment gateway request timed out or was cancelled. Please retry.");
            }

            throw;
        }
    }

    private async Task<NativeTopupInitiateResponse> InitiateCardTopupAsync(
        NativeTopupInitiateRequest request,
        StudentGuardianBasicDetailDto parentDetails,
        string orderId,
        int topupTransactionPkId,
        PaymentMethodEnum paymentMethod,
        decimal? minimumTopup,
        CancellationToken cancellationToken)
    {
        var paymentRequest = new StudentTopupPaymentRequest(request.StudentId.Trim(), request.Amount);
        var result = await _paymentGatewayRepository.CreateNativeTopupSessionAsync(
            paymentRequest,
            orderId,
            cancellationToken,
            request.ReturnUrl);

        _paymentBackgroundQueue.EnqueuePaymentLog(orderId, JsonSerializer.Serialize(result, JsonOptions));

        if (!result.IsSuccess)
        {
            await MarkTopupFailedAsync(
                topupTransactionPkId,
                parentDetails.GuardianId,
                result.TransactionId,
                string.IsNullOrWhiteSpace(result.Message) ? "Native payment session creation failed." : result.Message);

            return Fail(string.IsNullOrWhiteSpace(result.Message)
                ? "Unable to create native payment session."
                : result.Message);
        }

        await PersistTopupGatewayStateAsync(
            topupTransactionPkId,
            parentDetails,
            orderId,
            request.Amount,
            result.TransactionId,
            cancellationToken);

        return MapSessionResponse(result, paymentMethod, minimumTopup);
    }

    private async Task<NativeTopupInitiateResponse> InitiateWalletTopupAsync(
        NativeTopupInitiateRequest request,
        StudentGuardianBasicDetailDto parentDetails,
        string orderId,
        int topupTransactionPkId,
        int studentPk,
        PaymentMethodEnum paymentMethod,
        decimal? minimumTopup,
        CancellationToken cancellationToken)
    {
        var walletResult = await _paymentGatewayRepository.CreateWalletRegistrationAsync(
            new WalletRegistrationRequest
            {
                OrderId = orderId,
                Amount = request.Amount,
                OrderInfo = $"student topup {studentPk}",
                PaymentMethod = paymentMethod.ToApiString(),
                WalletName = paymentMethod == PaymentMethodEnum.SamsungPay ? "Samsung Pay" : "Apple Pay"
            },
            cancellationToken);

        _paymentBackgroundQueue.EnqueuePaymentLog(orderId, JsonSerializer.Serialize(walletResult, JsonOptions));

        if (!walletResult.IsSuccess)
        {
            await MarkTopupFailedAsync(
                topupTransactionPkId,
                parentDetails.GuardianId,
                walletResult.TransactionId,
                string.IsNullOrWhiteSpace(walletResult.Message) ? "Wallet registration failed." : walletResult.Message);

            return Fail(string.IsNullOrWhiteSpace(walletResult.Message)
                ? "Unable to create wallet payment session."
                : walletResult.Message);
        }

        await PersistTopupGatewayStateAsync(
            topupTransactionPkId,
            parentDetails,
            orderId,
            request.Amount,
            walletResult.TransactionId,
            cancellationToken);

        return new NativeTopupInitiateResponse
        {
            IsSuccess = true,
            Message = walletResult.Message,
            OrderId = walletResult.OrderId,
            TransactionId = walletResult.TransactionId,
            AuthenticationToken = walletResult.AuthenticationToken,
            MerchantUserName = walletResult.MerchantUserName,
            CustomerName = walletResult.CustomerName,
            BaseUrl = walletResult.BaseUrl,
            CallbackUrl = walletResult.CallbackUrl,
            Amount = walletResult.Amount,
            Currency = walletResult.Currency,
            PaymentMethod = paymentMethod.ToApiString(),
            SessionId = walletResult.SessionId,
            MerchantIdentifier = walletResult.MerchantIdentifier,
            SamsungPayMerchantId = walletResult.SamsungPayMerchantId,
            SamsungPayServiceId = walletResult.SamsungPayServiceId,
            MinimumTopupAmount = minimumTopup ?? 0m
        };
    }

    private async Task PersistTopupGatewayStateAsync(
        int topupTransactionPkId,
        StudentGuardianBasicDetailDto parentDetails,
        string orderId,
        decimal amount,
        string transactionId,
        CancellationToken cancellationToken)
    {
        await _transactionRepository.UpdateTopupTransactionStatusAsync(
            new TopupTransactionUpdateRequest
            {
                TransactionPkId = topupTransactionPkId,
                GatewayTransactionId = transactionId,
                StatusId = (int)TransactionStatusEnum.Initiated,
                IsTransactionCompleted = false,
                Remarks = orderId,
                UpdatedBy = parentDetails.GuardianId
            },
            cancellationToken);

        var requestObj = new
        {
            GUID = orderId,
            TransactionId = transactionId,
            GrdId = parentDetails.GuardianId,
            CustomerId = parentDetails.CustomerId,
            GuardianEmail = parentDetails.Email,
            Amount = amount.ToString(CultureInfo.InvariantCulture),
            TransactionType = "topup"
        };

        await _transactionRepository.InsertPendingTransactionAsync(
            new PendingTransactionRequest
            {
                CustomerID = parentDetails.CustomerId,
                Creby = parentDetails.Email,
                Amount = amount.ToString(CultureInfo.InvariantCulture),
                Loaded = "0",
                TransDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                Remarks = orderId,
                Mode = "O",
                BankName = "ETISALAT",
                PaymentDetails = transactionId,
                Billdate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                RequestObject = JsonSerializer.Serialize(requestObj)
            },
            cancellationToken);
    }

    private static NativeTopupInitiateResponse MapSessionResponse(
        NativePaymentSessionResult result,
        PaymentMethodEnum paymentMethod,
        decimal? minimumTopup) =>
        new()
        {
            IsSuccess = true,
            Message = result.Message,
            OrderId = result.OrderId,
            TransactionId = result.TransactionId,
            AuthenticationToken = result.AuthenticationToken,
            MerchantUserName = result.MerchantUserName,
            CustomerName = result.CustomerName,
            BaseUrl = result.BaseUrl,
            CallbackUrl = result.CallbackUrl,
            Amount = result.Amount,
            Currency = result.Currency,
            PaymentMethod = paymentMethod.ToApiString(),
            MinimumTopupAmount = minimumTopup ?? 0m
        };

    private async Task MarkTopupFailedAsync(
        int topupTransactionPkId,
        int guardianId,
        string? gatewayTransactionId,
        string remarks)
    {
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

    private static NativeTopupInitiateResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
