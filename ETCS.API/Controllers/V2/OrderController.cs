using Asp.Versioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers.V2;

[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/Order")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly INativeOrderInitiateService _nativeOrderInitiateService;
    private readonly INativeWalletRegistrationService _nativeWalletRegistrationService;
    private readonly IOrderPaymentCompleteService _orderPaymentCompleteService;

    public OrderController(
        INativeOrderInitiateService nativeOrderInitiateService,
        INativeWalletRegistrationService nativeWalletRegistrationService,
        IOrderPaymentCompleteService orderPaymentCompleteService)
    {
        _nativeOrderInitiateService = nativeOrderInitiateService;
        _nativeWalletRegistrationService = nativeWalletRegistrationService;
        _orderPaymentCompleteService = orderPaymentCompleteService;
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> InitiateNativeOrder(
        [FromBody] NativeOrderApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (request.StudentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        if (request.Total <= 0)
        {
            return BadRequest(new { message = "Total must be greater than zero." });
        }

        if (request.MealList is null || request.MealList.Count == 0)
        {
            return BadRequest(new { message = "MealList is required." });
        }

        var result = await _nativeOrderInitiateService.InitiateAsync(
            new NativeOrderInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                OrderTypeId = request.OrderTypeId,
                Total = request.Total,
                Notes = request.Notes,
                PaymentMethod = request.PaymentMethod,
                ReturnUrl = request.ReturnUrl,
                MealList = request.MealList
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("wallet/register")]
    public async Task<IActionResult> RegisterOrderWallet(
        [FromBody] NativeWalletApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _nativeWalletRegistrationService.RegisterAsync(
            new NativeWalletRegisterRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                OrderInfo = request.OrderInfo,
                ReturnUrl = request.ReturnUrl
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> CompleteNativeOrder(
        [FromBody] OrderCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (request.StudentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return BadRequest(new { message = "OrderId is required." });
        }

        var scopedRequest = new OrderCompleteRequest
        {
            StudentId = request.StudentId,
            GuardianId = guardianId,
            OrderId = request.OrderId,
            TransactionId = request.TransactionId
        };

        var result = await _orderPaymentCompleteService.CompleteAsync(scopedRequest, cancellationToken);
        if (!result.IsSuccess && !result.IsPending)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }
}

public sealed class NativeOrderApiRequest
{
    public int StudentId { get; init; }

    public int OrderTypeId { get; init; }

    public decimal Total { get; init; }

    public string Notes { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = "Card";

    public string? ReturnUrl { get; init; }

    public IReadOnlyList<OrderMealLineItemRequest> MealList { get; init; } = [];
}
