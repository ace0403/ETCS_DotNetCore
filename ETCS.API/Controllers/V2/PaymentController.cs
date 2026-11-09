using Asp.Versioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.PaymentGateway.Models;
using ETCS.PaymentGateway.Options;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Enumeration;
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
    private readonly INativePaymentService _nativePayments;
    private readonly PaymentGatewayOptions _paymentGatewayOptions;

    public PaymentController(
        INativeTopupInitiateService nativeTopupInitiateService,
        INativeWalletRegistrationService nativeWalletRegistrationService,
        INativePaymentService nativePayments,
        IOptions<PaymentGatewayOptions> paymentGatewayOptions)
    {
        _nativeTopupInitiateService = nativeTopupInitiateService;
        _nativeWalletRegistrationService = nativeWalletRegistrationService;
        _nativePayments = nativePayments;
        _paymentGatewayOptions = paymentGatewayOptions.Value;
    }

    [HttpPost("cancel")]
    public async Task<IActionResult> CancelNativePayment([FromBody] NativePaymentCancelApiRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId)) return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.TransactionId)
            || request.Type is not ("order" or "topup"))
            return BadRequest(new { message = "Exact original payment references and type are required." });
        var result = await _nativePayments.CancelAsync(guardianId, request.StudentId, request.OrderId.Trim(),
            request.TransactionId.Trim(), request.Type, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(new { message = result.Message });
    }

    // A missing SDK result is not a user cancellation. This route only verifies
    // the original payment and persists terminal evidence; it cannot Capture/Reverse/CancelPayment.
    [HttpPost("cancel/status")]
    public async Task<IActionResult> CheckNativeCancellation([FromBody] NativePaymentCancelApiRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId)) return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.OrderId) || string.IsNullOrWhiteSpace(request.TransactionId)
            || request.Type is not ("order" or "topup"))
            return BadRequest(new { message = "Exact original payment references and type are required." });
        var result = await _nativePayments.CancelAsync(guardianId, request.StudentId, request.OrderId.Trim(),
            request.TransactionId.Trim(), request.Type, cancellationToken, verifyOnly: true);
        return result.IsSuccess ? Ok(result) : BadRequest(new { message = result.Message });
    }

    [HttpPost("card/resume")]
    public async Task<IActionResult> ResumeNativeCard([FromBody] NativeCardResumeApiRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (request.StudentId <= 0 || string.IsNullOrWhiteSpace(request.OrderId) || request.Type is not ("topup" or "order"))
            return BadRequest(new { message = "Valid payment references and type are required." });
        if (!NativeReturnUrl.TryResolve(Request, request.ReturnUrl, _paymentGatewayOptions, out var returnUrl))
            return BadRequest(new { message = "A valid HTTPS native return URL on this API is required." });
        var result = await _nativePayments.ResumeCardAsync(guardianId, request.StudentId, request.OrderId.Trim(),
            request.TransactionId, request.Type, returnUrl, cancellationToken);
        return result.IsSuccess ? Ok(result) : BadRequest(new { message = result.Message });
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetNativeStatus(
        [FromQuery] string orderId, [FromQuery] int studentId,
        [FromQuery] string? transactionId = null, [FromQuery] string type = "order",
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (string.IsNullOrWhiteSpace(orderId) || studentId <= 0 || type is not ("order" or "topup"))
            return BadRequest(new { message = "A valid OrderId, StudentId and payment type are required." });
        var result = await _nativePayments.GetStatusAsync(
            guardianId, studentId, orderId.Trim(), transactionId, type, cancellationToken);
        if (!result.IsSuccess && !result.IsPending && result.Status is not ("declined" or "cancelled")) return BadRequest(new { message = result.Message });
        return Ok(result);
    }

    [HttpGet("native-capabilities")]
    public IActionResult GetNativeCapabilities()
    {
        return Ok(new NativePaymentCapabilitiesResponse
        {
            SamsungPayEnabled = _paymentGatewayOptions.NativeConfigurationError("SamsungPay") is null,
            ApplePayEnabled = _paymentGatewayOptions.NativeConfigurationError("ApplePay") is null
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

        if (!NativeReturnUrl.TryResolve(Request, request.ReturnUrl, _paymentGatewayOptions, out var returnUrl))
            return BadRequest(new { message = "A valid HTTPS native return URL on this API is required." });
        var result = await _nativeTopupInitiateService.InitiateAsync(
            new NativeTopupInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                Amount = request.Amount,
                PaymentMethod = request.PaymentMethod,
                ReturnUrl = returnUrl,
                SourceChannel = TransactionSourceChannel.Api.ToDbValue()
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

        if (!NativeReturnUrl.TryResolve(Request, request.ReturnUrl, _paymentGatewayOptions, out var returnUrl))
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
                PaymentKind = "topup",
                ReturnUrl = returnUrl
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
    [HttpPost("mobile-return")]
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
              <title>Payment authentication returned</title>
            </head>
            <body>
              <p>Authentication finished. Return to the app to confirm your payment.</p>
              <p><a href="{System.Net.WebUtility.HtmlEncode(deepLink)}">Return to the app</a></p>
            </body>
            </html>
            """;

        return Content(html, "text/html; charset=utf-8");
    }

    /// <summary>
    /// Finalizes native top-up with ownership checks, mobile credentials and retryable wallet credit.
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

        if (!User.TryGetGuardianId(out var guardianId))
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        if (request.StudentId <= 0)
            return BadRequest(new { message = "StudentId is required." });
        var result = await _nativePayments.CompleteTopupAsync(
            guardianId, request.StudentId, request.OrderId.Trim(), request.TransactionId, cancellationToken);
        if (!result.IsSuccess && !result.IsPending)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }
}

public sealed class NativePaymentCancelApiRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public int StudentId { get; init; }
    public string Type { get; init; } = "order";
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

public sealed class NativeCardResumeApiRequest
{
    public string OrderId { get; init; } = string.Empty;
    public string? TransactionId { get; init; }
    public int StudentId { get; init; }
    public string Type { get; init; } = "order";
    public string? ReturnUrl { get; init; }
}
