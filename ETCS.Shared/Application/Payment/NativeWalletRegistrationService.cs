using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using System.Text.Json;

namespace ETCS.Shared.Application.Payment;

public sealed class NativeWalletRegistrationService : INativeWalletRegistrationService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IStudentRepository _studentRepository;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly INativePaymentRepository _payments;
    private readonly ETCS.Shared.Infrastructure.Transaction.ITransactionRepository _transactions;

    public NativeWalletRegistrationService(
        IPaymentGatewayRepository paymentGatewayRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IStudentRepository studentRepository,
        IMealOrderRepository mealOrderRepository,
        INativePaymentRepository payments,
        ETCS.Shared.Infrastructure.Transaction.ITransactionRepository transactions)
    {
        _paymentGatewayRepository = paymentGatewayRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _studentRepository = studentRepository;
        _mealOrderRepository = mealOrderRepository;
        _payments = payments;
        _transactions = transactions;
    }

    public async Task<NativeWalletRegisterResponse> RegisterAsync(
        NativeWalletRegisterRequest request,
        CancellationToken cancellationToken)
    {
        if (request.GuardianId <= 0)
        {
            return Fail("Guardian is required.");
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Fail("OrderId is required.");
        }

        if (request.Amount <= 0)
        {
            return Fail("Amount must be greater than zero.");
        }

        var paymentMethod = PaymentMethodEnumExtensions.ParseOrUnknown(request.PaymentMethod);
        if (paymentMethod is not (PaymentMethodEnum.ApplePay or PaymentMethodEnum.SamsungPay))
        {
            return Fail("PaymentMethod must be ApplePay or SamsungPay.");
        }

        if (request.StudentId <= 0 || request.PaymentKind is not ("order" or "topup"))
            return Fail("StudentId and payment type are required.");

        if (request.StudentId > 0)
        {
            var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                request.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture),
                cancellationToken);
            if (guardianDetail is null || guardianDetail.GuardianId != request.GuardianId)
            {
                return Fail("You do not have access to this student.");
            }
        }

        await using var lease = await _payments.TryAcquireAsync(request.OrderId, request.PaymentKind, cancellationToken);
        if (lease is null) return Fail("Payment is still processing. Please retry.");
        var payment = await _payments.FindAsync(request.OrderId, request.PaymentKind, cancellationToken);
        if (payment is null || !payment.IsNative || payment.GuardianId != request.GuardianId
            || payment.StudentId != request.StudentId || payment.Amount != request.Amount
            || payment.PaymentMethod != (int)paymentMethod)
            return Fail("Payment not found or payment references do not match.");
        // Initiation already registers the chosen wallet. Never replace an in-flight or paid transaction.
        if (payment.IsPaid || payment.IsTransactionCompleted
            || !string.IsNullOrWhiteSpace(payment.GatewayTransactionId)
            || payment.StatusId is not ((int)TransactionStatusEnum.Pending or (int)TransactionStatusEnum.Initiated))
            return Fail("This payment already has a session. Use its existing session or start a new payment.");

        var walletResult = await _paymentGatewayRepository.CreateWalletRegistrationAsync(
            new WalletRegistrationRequest
            {
                OrderId = request.OrderId.Trim(),
                Amount = request.Amount,
                OrderInfo = string.IsNullOrWhiteSpace(request.OrderInfo) ? request.OrderId.Trim() : request.OrderInfo,
                PaymentMethod = paymentMethod.ToApiString(),
                WalletName = paymentMethod == PaymentMethodEnum.SamsungPay ? "Samsung Pay" : "Apple Pay",
                ReturnUrl = request.ReturnUrl
            },
            cancellationToken);

        _paymentBackgroundQueue.EnqueuePaymentLog(request.OrderId, JsonSerializer.Serialize(new { walletResult.IsSuccess, walletResult.Message, walletResult.OrderId, walletResult.TransactionId, walletResult.PaymentMethod }, JsonOptions));

        if (!walletResult.IsSuccess)
        {
            return Fail(string.IsNullOrWhiteSpace(walletResult.Message)
                ? "Wallet registration failed."
                : walletResult.Message);
        }

        await _payments.SaveSessionTermsAsync(new NativePaymentVerificationState
        {
            TransactionPkId = payment.TransactionPkId, OrderId = payment.OrderId, Kind = payment.Kind,
            GatewayTransactionId = walletResult.TransactionId, Amount = payment.Amount,
            Currency = walletResult.Currency.Trim().ToUpperInvariant(), ManualCapture = walletResult.ManualCapture
        }, cancellationToken);
        if (request.PaymentKind == "topup")
            await _transactions.UpdateTopupTransactionStatusAsync(
                new ETCS.Shared.Infrastructure.Transaction.TopupTransactionUpdateRequest
                {
                    TransactionPkId = payment.TransactionPkId, GatewayTransactionId = walletResult.TransactionId,
                    StatusId = (int)TransactionStatusEnum.Initiated, IsTransactionCompleted = false,
                    Remarks = payment.OrderId, UpdatedBy = payment.GuardianId
                }, cancellationToken);
        else
            await _mealOrderRepository.SetPaymentSessionAsync(payment.OrderId, walletResult.TransactionId,
                (int)TransactionStatusEnum.Initiated, cancellationToken);
        return new NativeWalletRegisterResponse
        {
            IsSuccess = true,
            Message = walletResult.Message,
            OrderId = walletResult.OrderId,
            TransactionId = walletResult.TransactionId,
            AuthenticationToken = walletResult.AuthenticationToken,
            SessionId = walletResult.SessionId,
            MerchantUserName = walletResult.MerchantUserName,
            CustomerName = walletResult.CustomerName,
            BaseUrl = walletResult.BaseUrl,
            CallbackUrl = walletResult.CallbackUrl,
            Amount = walletResult.Amount,
            Currency = walletResult.Currency,
            PaymentMethod = paymentMethod.ToApiString(),
            MerchantIdentifier = walletResult.MerchantIdentifier,
            SamsungPayMerchantId = walletResult.SamsungPayMerchantId,
            SamsungPayServiceId = walletResult.SamsungPayServiceId
        };
    }

    private static NativeWalletRegisterResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
