using Asp.Versioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Application.Topup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.API.Controllers.V2;

[ApiController]
[ApiVersion(2.0)]
[Route("api/v{version:apiVersion}/Payment")]
[Authorize]
public sealed class PaymentController : ControllerBase
{
    private readonly INativeTopupInitiateService _nativeTopupInitiateService;
    private readonly INativeWalletRegistrationService _nativeWalletRegistrationService;
    private readonly ITopupPaymentCompleteService _topupPaymentCompleteService;
    private readonly PaymentGatewayOptions _paymentGatewayOptions;

    public PaymentController(
        INativeTopupInitiateService nativeTopupInitiateService,
        INativeWalletRegistrationService nativeWalletRegistrationService,
        ITopupPaymentCompleteService topupPaymentCompleteService,
        IOptions<PaymentGatewayOptions> paymentGatewayOptions)
    {
        _nativeTopupInitiateService = nativeTopupInitiateService;
        _nativeWalletRegistrationService = nativeWalletRegistrationService;
        _topupPaymentCompleteService = topupPaymentCompleteService;
        _paymentGatewayOptions = paymentGatewayOptions.Value;
    }

    [HttpGet("native-capabilities")]
    public IActionResult GetNativeCapabilities()
    {
        return Ok(new NativePaymentCapabilitiesResponse
        {
            SamsungPayEnabled = !string.IsNullOrWhiteSpace(_paymentGatewayOptions.SamsungPayMerchantId)
                                && !string.IsNullOrWhiteSpace(_paymentGatewayOptions.SamsungPayServiceId),
            ApplePayEnabled = !string.IsNullOrWhiteSpace(_paymentGatewayOptions.ApplePayMerchantIdentifier)
        });
    }

    [HttpPost("topup/request")]
    public async Task<IActionResult> CreateNativeTopupSession(
        [FromBody] NativeTopupApiRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _nativeTopupInitiateService.InitiateAsync(
            new NativeTopupInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                ReturnUrl = request.ReturnUrl
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.MinimumTopupAmount is > 0)
            {
                return BadRequest(new
                {
                    message = result.Message,
                    minimumTopupAmount = result.MinimumTopupAmount
                });
            }

            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("topup/wallet/register")]
    public async Task<IActionResult> RegisterTopupWallet(
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

    /// <summary>
    /// Comtrust 3DS return URL for native EPG SDK sessions. Must be HTTPS so the SDK WebView can load it.
    /// </summary>
    [HttpGet("mobile-return")]
    [AllowAnonymous]
    public IActionResult MobileReturn([FromQuery] string orderid)
    {
        if (string.IsNullOrWhiteSpace(orderid))
        {
            return BadRequest("orderid is required.");
        }

        var encodedOrderId = Uri.EscapeDataString(orderid.Trim());
        var deepLink = $"nourixapp://payment-complete?orderid={encodedOrderId}";
        var html = $"""
            <!DOCTYPE html>
            <html lang="en">
            <head>
              <meta charset="utf-8" />
              <meta name="viewport" content="width=device-width, initial-scale=1" />
              <title>Payment complete</title>
            </head>
            <body>
              <p>Payment received. Returning to the app…</p>
              <script>window.location.replace({System.Text.Json.JsonSerializer.Serialize(deepLink)});</script>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Finalizes native top-up by delegating to the existing v1 completion pipeline.
    /// </summary>
    [HttpPost("topup/complete")]
    public async Task<IActionResult> CompleteNativeTopup(
        [FromBody] PaymentCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return BadRequest(new { message = "OrderId is required." });
        }

        var result = await _topupPaymentCompleteService.CompleteAsync(
            new TopupCompleteRequest
            {
                StudentId = request.StudentId,
                OrderId = request.OrderId,
                TransactionId = request.TransactionId ?? string.Empty
            },
            cancellationToken);

        if (!result.IsSuccess && !result.IsPending)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(new PaymentCaptureResult
        {
            IsSuccess = result.IsSuccess,
            IsPending = result.IsPending,
            Message = result.Message,
            TransactionId = result.TransactionId,
            Status = result.Status
        });
    }
}

public sealed class NativeTopupApiRequest
{
    public string StudentId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string PaymentMethod { get; init; } = "Card";

    public string? ReturnUrl { get; init; }
}

public sealed class NativeWalletApiRequest
{
    public int StudentId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string PaymentMethod { get; init; } = string.Empty;

    public string OrderInfo { get; init; } = string.Empty;

    public string? ReturnUrl { get; init; }
}

public sealed class NativePaymentCapabilitiesResponse
{
    public bool SamsungPayEnabled { get; init; }

    public bool ApplePayEnabled { get; init; }
}
