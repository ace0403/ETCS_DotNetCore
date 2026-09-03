using Asp.Versioning;
using ETCS.API.Infrastructure.Auth;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Payment;
using ETCS.Shared.Application.Topup;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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

    public PaymentController(
        INativeTopupInitiateService nativeTopupInitiateService,
        INativeWalletRegistrationService nativeWalletRegistrationService,
        ITopupPaymentCompleteService topupPaymentCompleteService)
    {
        _nativeTopupInitiateService = nativeTopupInitiateService;
        _nativeWalletRegistrationService = nativeWalletRegistrationService;
        _topupPaymentCompleteService = topupPaymentCompleteService;
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
                OrderInfo = request.OrderInfo
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
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
}
