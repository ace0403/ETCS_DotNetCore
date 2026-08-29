using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using System.Globalization;
using System.Text.Json;

namespace ETCS.Shared.Application.Orders;

public sealed class OrderInitiateService : IOrderInitiateService
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;
    private readonly ISchoolCalendarService _schoolCalendar;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public OrderInitiateService(
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

    public async Task<OrderInitiateResponse> InitiateAsync(OrderInitiateRequest request, CancellationToken cancellationToken)
    {
        if (!await _orderTypeAccess.IsAllowedAsync(request.StudentId, request.OrderTypeId, cancellationToken))
        {
            return new OrderInitiateResponse
            {
                IsSuccess = false,
                Message = _orderTypeAccess.GetDeniedMessage(request.OrderTypeId)
            };
        }

        var generatedOrderId = OrderIdGenerator.GenerateForStudent(request.StudentId);

        var lineTotal = request.MealList.Sum(x => x.Price);
        if (Math.Abs(lineTotal - request.Total) > 0.01m)
        {
            return new OrderInitiateResponse
            {
                IsSuccess = false,
                Message = "Total does not match sum of meal item prices."
            };
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(request.StudentId, cancellationToken);
        if (schoolId is > 0)
        {
            foreach (var meal in request.MealList.OrderBy(x => x.MealDate))
            {
                if (!await _schoolCalendar.IsOrderableAsync(schoolId.Value, meal.MealDate.Date, cancellationToken))
                {
                    var day = await _schoolCalendar.GetDayInfoAsync(schoolId.Value, meal.MealDate.Date, cancellationToken);
                    return new OrderInitiateResponse
                    {
                        IsSuccess = false,
                        Message = day.GetClosedOrderMessage(meal.MealDate.Date)
                    };
                }
            }
        }

        var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
        if (guardianDetail is null || string.IsNullOrWhiteSpace(guardianDetail.CustomerId))
        {
            return new OrderInitiateResponse
            {
                IsSuccess = false,
                Message = "Unable to resolve customer profile for this student."
            };
        }

        var mealTransactionId = await _mealOrderRepository.CreatePendingOrderAsync(
            new OrderInitiateRequest
            {
                StudentId = request.StudentId,
                GuardianId = request.GuardianId,
                OrderId = generatedOrderId,
                OrderStatusId = (int)TransactionStatusEnum.Pending,
                OrderTypeId = request.OrderTypeId,
                Total = request.Total,
                Notes = request.Notes,
                MealList = request.MealList
            },
            (int)TransactionStatusEnum.Initiated,
            cancellationToken);

        var sessionResult = await _paymentGatewayRepository.CreateOrderSessionAsync(
            new OrderPaymentSessionRequest
            {
                StudentId = request.StudentId,
                GuardianId = request.GuardianId,
                OrderId = generatedOrderId,
                Total = request.Total,
                Notes = request.Notes
            },
            cancellationToken);

        var pgResponse = JsonSerializer.Serialize(sessionResult, JsonOptions);
        _paymentBackgroundQueue.EnqueuePaymentLog(generatedOrderId, pgResponse ?? string.Empty);

        if (!sessionResult.IsSuccess)
        {
            await _mealOrderRepository.SetPaymentSessionFailedAsync(
                generatedOrderId,
                sessionResult.Message,
                cancellationToken);

            return new OrderInitiateResponse
            {
                IsSuccess = false,
                Message = string.IsNullOrWhiteSpace(sessionResult.Message)
                    ? "Unable to initiate payment session."
                    : sessionResult.Message,
                OrderId = generatedOrderId,
                StudentId = request.StudentId,
                GuardianId = request.GuardianId,
                Total = request.Total,
                MealTransactionId = mealTransactionId
            };
        }

        await _mealOrderRepository.SetPaymentSessionAsync(
            generatedOrderId,
            sessionResult.TransactionId,
            (int)TransactionStatusEnum.Initiated,
            cancellationToken);

        var orderRequestObject = new
        {
            GUID = generatedOrderId,
            TransactionId = sessionResult.TransactionId,
            GrdId = guardianDetail.GuardianId,
            CustomerId = guardianDetail.CustomerId,
            GuardianEmail = guardianDetail.Email,
            Amount = request.Total.ToString(CultureInfo.InvariantCulture),
            TransactionType = "order"
        };

        await _transactionRepository.InsertPendingTransactionAsync(
            new PendingTransactionRequest
            {
                CustomerID = orderRequestObject.CustomerId,
                Creby = guardianDetail.Email,
                Amount = request.Total.ToString(CultureInfo.InvariantCulture),
                Loaded = "0",
                TransDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                Remarks = generatedOrderId,
                Mode = "O",
                BankName = "ETISALAT",
                PaymentDetails = sessionResult.TransactionId,
                Billdate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                RequestObject = JsonSerializer.Serialize(orderRequestObject)
            },
            cancellationToken);

        return new OrderInitiateResponse
        {
            IsSuccess = true,
            Message = "Order initiated successfully.",
            OrderId = generatedOrderId,
            StudentId = request.StudentId,
            GuardianId = request.GuardianId,
            Total = request.Total,
            MealTransactionId = mealTransactionId,
            PaymentUrl = sessionResult.RedirectUrl,
            GatewayTransactionId = sessionResult.TransactionId
        };
    }
}
