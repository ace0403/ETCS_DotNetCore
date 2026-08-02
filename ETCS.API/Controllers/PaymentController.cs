using ETCS.API.Features.Payment;
using ETCS.API.Infrastructure.Auth;
using ETCS.Shared.Application.Topup;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PaymentController : ControllerBase
{
    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITopupInitiateService _topupInitiateService;
    private readonly ITopupPaymentCompleteService _topupPaymentCompleteService;
    private readonly IPaymentStatusService _paymentStatusService;

    public PaymentController(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository,
        ITopupInitiateService topupInitiateService,
        ITopupPaymentCompleteService topupPaymentCompleteService,
        IPaymentStatusService paymentStatusService)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
        _topupInitiateService = topupInitiateService;
        _topupPaymentCompleteService = topupPaymentCompleteService;
        _paymentStatusService = paymentStatusService;
    }

    /// <summary>
    /// Returns the minimum top-up amount configured for the student's school.
    /// </summary>
    [HttpGet("topup/minimum")]
    public async Task<IActionResult> GetTopupMinimum(
        [FromQuery] string studentId,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (string.IsNullOrWhiteSpace(studentId))
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        if (!int.TryParse(studentId.Trim(), out var studentPk) || studentPk <= 0)
        {
            return BadRequest(new { message = "StudentId is invalid." });
        }

        var parentDetails = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(studentId.Trim(), cancellationToken);
        if (parentDetails is null)
        {
            return NotFound(new { message = "Student was not found." });
        }

        if (parentDetails.GuardianId != guardianId)
        {
            return Forbid();
        }

        var minimumTopup = await _studentRepository.GetStudentMinimumTopupAsync(studentPk, cancellationToken) ?? 0m;

        return Ok(new StudentTopupMinimumDto
        {
            StudentId = studentId.Trim(),
            MinimumTopupAmount = minimumTopup
        });
    }

    /// <summary>
    /// Creates a payment session for student topup and returns redirect details.
    /// </summary>
    [HttpPost("topup/request")]
    public async Task<IActionResult> CreateStudentTopupSession(
        [FromBody] StudentTopupPaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        var result = await _topupInitiateService.InitiateAsync(
            new TopupInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = request.StudentId,
                Amount = request.Amount
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

        return Ok(new PaymentSessionCreateResult
        {
            IsSuccess = true,
            Message = result.Message,
            TransactionId = result.TransactionId,
            RedirectUrl = result.RedirectUrl,
            OrderId = result.OrderId
        });
    }

    /// <summary>
    /// Captures/finalizes payment status using transaction reference.
    /// </summary>
    [HttpPost("topup/update")]
    public async Task<IActionResult> CaptureTopupPayment(
        [FromBody] PaymentCaptureRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { message = "TransactionId is required." });
        }

        var result = await _topupPaymentCompleteService.CompleteAsync(
            new TopupCompleteRequest
            {
                StudentId = request.StudentId,
                OrderId = request.OrderId,
                TransactionId = request.TransactionId
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

    /// <summary>
    /// Read-only payment status check. Does not finalize wallet or order; use complete/update for that.
    /// </summary>
    [HttpGet("status")]
    public async Task<IActionResult> GetPaymentStatus(
        [FromQuery] string orderId,
        [FromQuery] string transactionId,
        [FromQuery] int studentId,
        [FromQuery] string type = "order",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new { message = "OrderId is required." });
        }

        if (string.IsNullOrWhiteSpace(transactionId))
        {
            return BadRequest(new { message = "TransactionId is required." });
        }

        if (studentId <= 0)
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        var status = await _paymentStatusService.GetStatusAsync(
            orderId.Trim(),
            transactionId.Trim(),
            studentId,
            type,
            cancellationToken);

        return Ok(status);
    }

    /// <summary>
    /// Last N transactions for the logged-in guardian (default 5). JWT required.
    /// </summary>
    [HttpGet("transactions/recent")]
    public async Task<IActionResult> GetRecentTransactions(
        [FromQuery] int? studentId,
        [FromQuery] string? type = "all",
        [FromQuery] int count = 5,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (count <= 0 || count > 50)
        {
            return BadRequest(new { message = "Count must be between 1 and 50." });
        }

        TransactionHistoryResponse history;
        try
        {
            history = await _transactionRepository.GetTransactionHistoryAsync(
                studentId,
                guardianId,
                type,
                fromDate: null,
                toDate: null,
                page: 1,
                pageSize: count,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(new RecentTransactionsResponse
        {
            GuardianId = guardianId,
            Count = history.Items.Count,
            Items = history.Items
        });
    }

    /// <summary>
    /// Gets paginated topup transaction history.
    /// </summary>
    [HttpGet("transactions/history")]
    public async Task<IActionResult> GetTransactionHistory(
        [FromQuery] int? studentId,
        [FromQuery] string? type = "all",
        [FromQuery] DateTime? fromDate = null,
        [FromQuery] DateTime? toDate = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (page <= 0)
        {
            return BadRequest(new { message = "Page must be greater than zero." });
        }

        if (pageSize <= 0 || pageSize > 200)
        {
            return BadRequest(new { message = "PageSize must be between 1 and 200." });
        }

        TransactionHistoryResponse result;
        try
        {
            result = await _transactionRepository.GetTransactionHistoryAsync(
                studentId,
                guardianId,
                type,
                fromDate,
                toDate,
                page,
                pageSize,
                cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        return Ok(result);
    }
}
