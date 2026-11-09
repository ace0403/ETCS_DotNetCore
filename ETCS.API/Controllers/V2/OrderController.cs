using Asp.Versioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.PaymentGateway.Options;
using Microsoft.Extensions.Options;
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
    private readonly INativePaymentService _nativePayments;
    private readonly OrderAllergenConsentEnricher _consent;
    private readonly IStudentRepository _students;
    private readonly PaymentGatewayOptions _options;

    public OrderController(
        INativeOrderInitiateService nativeOrderInitiateService,
        INativeWalletRegistrationService nativeWalletRegistrationService,
        INativePaymentService nativePayments,
        OrderAllergenConsentEnricher consent,
        IStudentRepository students,
        IOptions<PaymentGatewayOptions> options)
    {
        _nativeOrderInitiateService = nativeOrderInitiateService;
        _nativeWalletRegistrationService = nativeWalletRegistrationService;
        _nativePayments = nativePayments;
        _consent = consent;
        _students = students;
        _options = options.Value;
    }

    [HttpPost("allergen-consent")]
    public async Task<IActionResult> GetAllergenConsent(
        [FromBody] NativeOrderApiRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (request.StudentId <= 0 || request.MealList is null || request.MealList.Count is 0 or > 500)
            return BadRequest(new { message = "StudentId and meal lines are required." });
        var guardian = await _students.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.ToString(System.Globalization.CultureInfo.InvariantCulture), cancellationToken);
        if (guardian?.GuardianId != guardianId) return Forbid();
        var map = await _consent.ResolveItemTextMapAsync(request.StudentId,
            request.MealList.Select(x => (x.ItemId, x.PackageId)), cancellationToken);
        return Ok(map.Where(x => !string.IsNullOrWhiteSpace(x.Value)).Select(x => new
        {
            x.Key.ItemId, x.Key.PackageId, AllergenText = x.Value
        }));
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

        if (!NativeReturnUrl.TryResolve(Request, request.ReturnUrl, _options, out var returnUrl))
            return BadRequest(new { message = "A valid HTTPS native return URL on this API is required." });
        var result = await _nativeOrderInitiateService.InitiateAsync(
            new NativeOrderInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                OrderTypeId = request.OrderTypeId,
                Total = request.Total,
                Notes = request.Notes,
                PaymentMethod = request.PaymentMethod,
                ReturnUrl = returnUrl,
                SourceChannel = TransactionSourceChannel.Api.ToDbValue(),
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

        if (!NativeReturnUrl.TryResolve(Request, request.ReturnUrl, _options, out var returnUrl))
            return BadRequest(new { message = "A valid HTTPS native return URL on this API is required." });
        var result = await _nativeWalletRegistrationService.RegisterAsync(
            new NativeWalletRegisterRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                OrderId = request.OrderId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                OrderInfo = request.OrderInfo,
                PaymentKind = "order",
                ReturnUrl = returnUrl
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

        var result = await _nativePayments.CompleteOrderAsync(
            guardianId, request.StudentId, request.OrderId.Trim(), request.TransactionId, cancellationToken);
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
