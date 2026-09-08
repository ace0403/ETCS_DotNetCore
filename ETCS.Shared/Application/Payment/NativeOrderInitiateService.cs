using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using System.Globalization;
using System.Text.Json;

namespace ETCS.Shared.Application.Payment;

public sealed class NativeOrderInitiateService : INativeOrderInitiateService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;
    private readonly ISchoolCalendarService _schoolCalendar;

    public NativeOrderInitiateService(
        IMealOrderRepository mealOrderRepository,
        IStudentRepository studentRepository,
        IPaymentGatewayRepository paymentGatewayRepository,
        ITransactionRepository transactionRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IStudentOrderTypeAccessService orderTypeAccess,
        ISchoolCalendarService schoolCalendar)
    {
        _mealOrderRepository = mealOrderRepository;
        _studentRepository = studentRepository;
        _paymentGatewayRepository = paymentGatewayRepository;
        _transactionRepository = transactionRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _orderTypeAccess = orderTypeAccess;
        _schoolCalendar = schoolCalendar;
    }

    public async Task<NativeOrderInitiateResponse> InitiateAsync(
        NativeOrderInitiateRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _orderTypeAccess.IsAllowedAsync(request.StudentId, request.OrderTypeId, cancellationToken))
        {
            return Fail(_orderTypeAccess.GetDeniedMessage(request.OrderTypeId));
        }

        var paymentMethod = PaymentMethodEnumExtensions.ParseOrUnknown(request.PaymentMethod);
        if (paymentMethod is PaymentMethodEnum.Unknown)
        {
            paymentMethod = PaymentMethodEnum.Card;
        }

        var generatedOrderId = OrderIdGenerator.GenerateForStudent(request.StudentId);
        var lineTotal = request.MealList.Sum(x => x.Price);
        if (Math.Abs(lineTotal - request.Total) > 0.01m)
        {
            return Fail("Total does not match sum of meal item prices.");
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(request.StudentId, cancellationToken);
        if (schoolId is > 0)
        {
            foreach (var meal in request.MealList.OrderBy(x => x.MealDate))
            {
                if (!await _schoolCalendar.IsOrderableAsync(schoolId.Value, meal.MealDate.Date, cancellationToken))
                {
                    var day = await _schoolCalendar.GetDayInfoAsync(schoolId.Value, meal.MealDate.Date, cancellationToken);
                    return Fail(day.GetClosedOrderMessage(meal.MealDate.Date));
                }
            }
        }

        var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
        if (guardianDetail is null || string.IsNullOrWhiteSpace(guardianDetail.CustomerId))
        {
            return Fail("Unable to resolve customer profile for this student.");
        }

        var orderRequest = new OrderInitiateRequest
        {
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            OrderId = generatedOrderId,
            OrderStatusId = (int)TransactionStatusEnum.Pending,
            OrderTypeId = request.OrderTypeId,
            Total = request.Total,
            Notes = request.Notes,
            MealList = request.MealList
        };

        var mealTransactionId = await _mealOrderRepository.CreatePendingOrderAsync(
            orderRequest,
            (int)TransactionStatusEnum.Initiated,
            cancellationToken,
            (int)paymentMethod);

        if (paymentMethod is PaymentMethodEnum.ApplePay or PaymentMethodEnum.SamsungPay)
        {
            var walletResult = await _paymentGatewayRepository.CreateWalletRegistrationAsync(
                new WalletRegistrationRequest
                {
                    OrderId = generatedOrderId,
                    Amount = request.Total,
                    OrderInfo = request.Notes,
                    PaymentMethod = paymentMethod.ToApiString(),
                    WalletName = paymentMethod == PaymentMethodEnum.SamsungPay ? "Samsung Pay" : "Apple Pay",
                    ReturnUrl = request.ReturnUrl
                },
                cancellationToken);

            _paymentBackgroundQueue.EnqueuePaymentLog(generatedOrderId, JsonSerializer.Serialize(walletResult, JsonOptions));

            if (!walletResult.IsSuccess)
            {
                await _mealOrderRepository.SetPaymentSessionFailedAsync(
                    generatedOrderId,
                    walletResult.Message,
                    cancellationToken);

                return Fail(string.IsNullOrWhiteSpace(walletResult.Message)
                    ? "Unable to initiate wallet payment session."
                    : walletResult.Message);
            }

            await _mealOrderRepository.SetPaymentSessionAsync(
                generatedOrderId,
                walletResult.TransactionId,
                (int)TransactionStatusEnum.Initiated,
                cancellationToken);

            await InsertPendingOrderRecordAsync(guardianDetail, generatedOrderId, request, walletResult.TransactionId, cancellationToken);

            return MapWalletOrderResponse(request, generatedOrderId, mealTransactionId, walletResult, paymentMethod);
        }

        var sessionResult = await _paymentGatewayRepository.CreateNativeOrderSessionAsync(
            new OrderPaymentSessionRequest
            {
                StudentId = request.StudentId,
                GuardianId = request.GuardianId,
                OrderId = generatedOrderId,
                Total = request.Total,
                Notes = request.Notes,
                ReturnUrl = request.ReturnUrl
            },
            cancellationToken);

        _paymentBackgroundQueue.EnqueuePaymentLog(generatedOrderId, JsonSerializer.Serialize(sessionResult, JsonOptions));

        if (!sessionResult.IsSuccess)
        {
            await _mealOrderRepository.SetPaymentSessionFailedAsync(
                generatedOrderId,
                sessionResult.Message,
                cancellationToken);

            return Fail(string.IsNullOrWhiteSpace(sessionResult.Message)
                ? "Unable to initiate native payment session."
                : sessionResult.Message);
        }

        await _mealOrderRepository.SetPaymentSessionAsync(
            generatedOrderId,
            sessionResult.TransactionId,
            (int)TransactionStatusEnum.Initiated,
            cancellationToken);

        await InsertPendingOrderRecordAsync(guardianDetail, generatedOrderId, request, sessionResult.TransactionId, cancellationToken);

        return new NativeOrderInitiateResponse
        {
            IsSuccess = true,
            Message = "Order initiated successfully.",
            OrderId = generatedOrderId,
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            Total = request.Total,
            MealTransactionId = mealTransactionId,
            TransactionId = sessionResult.TransactionId,
            AuthenticationToken = sessionResult.AuthenticationToken,
            MerchantUserName = sessionResult.MerchantUserName,
            CustomerName = sessionResult.CustomerName,
            BaseUrl = sessionResult.BaseUrl,
            CallbackUrl = sessionResult.CallbackUrl,
            Currency = sessionResult.Currency,
            PaymentMethod = paymentMethod.ToApiString()
        };
    }

    private async Task InsertPendingOrderRecordAsync(
        StudentGuardianBasicDetailDto guardianDetail,
        string generatedOrderId,
        NativeOrderInitiateRequest request,
        string transactionId,
        CancellationToken cancellationToken)
    {
        var orderRequestObject = new
        {
            GUID = generatedOrderId,
            TransactionId = transactionId,
            GrdId = guardianDetail.GuardianId,
            CustomerId = guardianDetail.CustomerId,
            GuardianEmail = guardianDetail.Email,
            Amount = request.Total.ToString(CultureInfo.InvariantCulture),
            TransactionType = "order"
        };

        await _transactionRepository.InsertPendingTransactionAsync(
            new PendingTransactionRequest
            {
                CustomerID = guardianDetail.CustomerId,
                Creby = guardianDetail.Email,
                Amount = request.Total.ToString(CultureInfo.InvariantCulture),
                Loaded = "0",
                TransDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                Remarks = generatedOrderId,
                Mode = "O",
                BankName = "ETISALAT",
                PaymentDetails = transactionId,
                Billdate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                RequestObject = JsonSerializer.Serialize(orderRequestObject)
            },
            cancellationToken);
    }

    private static NativeOrderInitiateResponse MapWalletOrderResponse(
        NativeOrderInitiateRequest request,
        string generatedOrderId,
        int mealTransactionId,
        NativeWalletSessionResult walletResult,
        PaymentMethodEnum paymentMethod) =>
        new()
        {
            IsSuccess = true,
            Message = "Order initiated successfully.",
            OrderId = generatedOrderId,
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            Total = request.Total,
            MealTransactionId = mealTransactionId,
            TransactionId = walletResult.TransactionId,
            AuthenticationToken = walletResult.AuthenticationToken,
            MerchantUserName = walletResult.MerchantUserName,
            CustomerName = walletResult.CustomerName,
            BaseUrl = walletResult.BaseUrl,
            CallbackUrl = walletResult.CallbackUrl,
            Currency = walletResult.Currency,
            PaymentMethod = paymentMethod.ToApiString(),
            SessionId = walletResult.SessionId,
            MerchantIdentifier = walletResult.MerchantIdentifier,
            SamsungPayMerchantId = walletResult.SamsungPayMerchantId,
            SamsungPayServiceId = walletResult.SamsungPayServiceId
        };

    private static NativeOrderInitiateResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
