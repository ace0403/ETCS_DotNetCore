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

    public NativeWalletRegistrationService(
        IPaymentGatewayRepository paymentGatewayRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IStudentRepository studentRepository,
        IMealOrderRepository mealOrderRepository)
    {
        _paymentGatewayRepository = paymentGatewayRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _studentRepository = studentRepository;
        _mealOrderRepository = mealOrderRepository;
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

        _paymentBackgroundQueue.EnqueuePaymentLog(request.OrderId, JsonSerializer.Serialize(walletResult, JsonOptions));

        if (!walletResult.IsSuccess)
        {
            await _mealOrderRepository.SetPaymentSessionFailedAsync(
                request.OrderId.Trim(),
                walletResult.Message,
                cancellationToken);

            return Fail(string.IsNullOrWhiteSpace(walletResult.Message)
                ? "Wallet registration failed."
                : walletResult.Message);
        }

        await _mealOrderRepository.SetPaymentSessionAsync(
            request.OrderId.Trim(),
            walletResult.TransactionId,
            (int)TransactionStatusEnum.Initiated,
            cancellationToken);

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
