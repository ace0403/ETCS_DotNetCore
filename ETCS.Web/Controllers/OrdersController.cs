using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Orders;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

public sealed class OrdersController : Controller
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IOrderPaymentCompleteService _orderPaymentCompleteService;
    private readonly OrderPaymentSummaryBuilder _summaryBuilder;

    public OrdersController(
        IMealOrderRepository mealOrderRepository,
        IOrderPaymentCompleteService orderPaymentCompleteService,
        OrderPaymentSummaryBuilder summaryBuilder)
    {
        _mealOrderRepository = mealOrderRepository;
        _orderPaymentCompleteService = orderPaymentCompleteService;
        _summaryBuilder = summaryBuilder;
    }

    [HttpGet]
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PaymentReturn(
        [FromQuery] string? orderId,
        [FromForm] ComtrustCallbackRequest? callback,
        CancellationToken cancellationToken)
    {
        var resolvedOrderId = OrderPaymentSummaryBuilder.ResolvePaymentReturnOrderId(orderId, callback);
        if (string.IsNullOrWhiteSpace(resolvedOrderId))
        {
            return View(new OrderPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Order reference is missing."
            });
        }

        var paymentState = await _mealOrderRepository.GetPaymentStateAsync(resolvedOrderId, cancellationToken);
        if (paymentState is null)
        {
            return View(new OrderPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Order was not found.",
                OrderId = resolvedOrderId
            });
        }

        if (User.TryGetGuardianId(out var sessionGuardianId) && sessionGuardianId != paymentState.GuardianId)
        {
            return View(new OrderPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Order was not found.",
                OrderId = resolvedOrderId
            });
        }

        var guardianId = paymentState.GuardianId;
        var gatewayTransactionId = await _mealOrderRepository.GetGatewayTransactionIdByOrderIdAsync(resolvedOrderId, cancellationToken);
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            gatewayTransactionId = callback?.TransactionID?.Trim();
        }

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return View(new OrderPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Payment session was not found.",
                OrderId = resolvedOrderId,
                OrderTypeId = paymentState.OrderTypeId
            });
        }

        var completeResult = await _orderPaymentCompleteService.CompleteAsync(
            new OrderCompleteRequest
            {
                StudentId = paymentState.StudentId,
                GuardianId = guardianId,
                OrderId = resolvedOrderId,
                TransactionId = gatewayTransactionId
            },
            cancellationToken);

        var model = await _summaryBuilder.BuildReceiptAsync(
            guardianId,
            paymentState.OrderTypeId,
            resolvedOrderId,
            completeResult.IsSuccess,
            completeResult.IsPending,
            completeResult.Message,
            cancellationToken);

        return View(model);
    }
}
