using ETCS.API.Features.Payment;
using ETCS.API.Infrastructure.Auth;
using ETCS.API.Infrastructure.Background;
using ETCS.Shared.Application.Background;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Application.Payment;
using ETCS.PaymentGateway.Abstractions;
using ETCS.PaymentGateway.Models;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text.Json;

namespace ETCS.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public sealed class PaymentController : ControllerBase
{
    private readonly IPaymentGatewayRepository _paymentGatewayRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly IPaymentBackgroundQueue _paymentBackgroundQueue;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly PaymentCompletionCancellation _completionCancellation;
    private readonly IPaymentStatusService _paymentStatusService;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PaymentController(
        IPaymentGatewayRepository paymentGatewayRepository,
        ITransactionRepository transactionRepository,
        IStudentRepository studentRepository,
        IPaymentBackgroundQueue paymentBackgroundQueue,
        IGuardianEmailNotificationService emailNotificationService,
        PaymentCompletionCancellation completionCancellation,
        IPaymentStatusService paymentStatusService)
    {
        _paymentGatewayRepository = paymentGatewayRepository;
        _transactionRepository = transactionRepository;
        _studentRepository = studentRepository;
        _paymentBackgroundQueue = paymentBackgroundQueue;
        _emailNotificationService = emailNotificationService;
        _completionCancellation = completionCancellation;
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

        if (string.IsNullOrWhiteSpace(request.StudentId))
        {
            return BadRequest(new { message = "StudentId is required." });
        }

        if (!int.TryParse(request.StudentId.Trim(), out var studentPk) || studentPk <= 0)
        {
            return BadRequest(new { message = "StudentId is invalid." });
        }

        if (request.Amount <= 0)
        {
            return BadRequest(new { message = "Amount must be greater than zero." });
        }

        var parentDetails = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(request.StudentId, cancellationToken);
        if (parentDetails is null)
        {
            return BadRequest(new { message = "Unable to resolve guardian details for this student." });
        }

        if (parentDetails.GuardianId != guardianId)
        {
            return Forbid();
        }

        var minimumTopup = await _studentRepository.GetStudentMinimumTopupAsync(studentPk, cancellationToken);
        if (!TopupAmountRules.MeetsMinimum(request.Amount, minimumTopup))
        {
            var minimum = minimumTopup ?? 0m;
            return BadRequest(new
            {
                message = $"Minimum top-up amount for this student is {minimum.ToString("F2", CultureInfo.InvariantCulture)}.",
                minimumTopupAmount = minimum
            });
        }

        var orderId = OrderIdGenerator.GenerateForStudent(request.StudentId);
        var topupTransactionPkId = await _transactionRepository.CreateTopupPendingTransactionAsync(
            new TopupTransactionCreateRequest
            {
                GuardianId = parentDetails.GuardianId,
                StudentId = studentPk,
                Amount = request.Amount,
                Remarks = orderId,
                StatusId = (int)TransactionStatusEnum.Pending,
                CreatedBy = parentDetails.GuardianId
            },
            cancellationToken);

        var result = await _paymentGatewayRepository.CreateTopupSessionAsync(request, orderId, cancellationToken);

        var pgResponse = JsonSerializer.Serialize(result, JsonOptions);
        _paymentBackgroundQueue.EnqueuePaymentLog(
            orderId,
            pgResponse ?? string.Empty);

        if (!result.IsSuccess)
        {
            await _transactionRepository.UpdateTopupTransactionStatusAsync(
                new TopupTransactionUpdateRequest
                {
                    TransactionPkId = topupTransactionPkId,
                    GatewayTransactionId = string.IsNullOrWhiteSpace(result.TransactionId) ? string.Empty : result.TransactionId,
                    StatusId = (int)TransactionStatusEnum.Failed,
                    IsTransactionCompleted = false,
                    Remarks = string.IsNullOrWhiteSpace(result.Message) ? "Payment session creation failed." : result.Message,
                    UpdatedBy = parentDetails.GuardianId
                },
                cancellationToken);

            return BadRequest(new
            {
                message = string.IsNullOrWhiteSpace(result.Message)
                    ? "Unable to create payment session."
                    : result.Message
            });
        }

        await _transactionRepository.UpdateTopupTransactionStatusAsync(
            new TopupTransactionUpdateRequest
            {
                TransactionPkId = topupTransactionPkId,
                GatewayTransactionId = result.TransactionId,
                StatusId = (int)TransactionStatusEnum.Initiated,
                IsTransactionCompleted = false,
                Remarks = "Payment session created.",
                UpdatedBy = parentDetails.GuardianId
            },
            cancellationToken);

        var requestObj = new
        {
            GUID = orderId,
            TransactionId = result.TransactionId,
            GrdId = parentDetails?.GuardianId ?? 0,
            CustomerId = parentDetails?.CustomerId ?? string.Empty,
            GuardianEmail = parentDetails?.Email ?? string.Empty,
            Amount = request.Amount.ToString(),
            TransactionType = "topup"
        };

        await _transactionRepository.InsertPendingTransactionAsync(
            new PendingTransactionRequest
            {
                CustomerID = requestObj.CustomerId,
                Creby = parentDetails?.Email ?? string.Empty,
                Amount = request.Amount.ToString(CultureInfo.InvariantCulture),
                Loaded = "0",
                TransDate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                Remarks = orderId,
                Mode = "O",
                BankName = "ETISALAT",
                PaymentDetails = result.TransactionId,
                Billdate = DateTime.Now.ToString("dd-MM-yyyy hh:mm:ss tt", CultureInfo.InvariantCulture),
                RequestObject = JsonSerializer.Serialize(requestObj)
            },
            cancellationToken);

        return Ok(result);
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

        var topupState = await _transactionRepository.GetTopupPendingForCompletionAsync(
            request.OrderId,
            request.TransactionId,
            cancellationToken);

        if (topupState is { IsTransactionCompleted: true })
        {
            return Ok(new PaymentCaptureResult
            {
                IsSuccess = true,
                Message = "Top-up already completed.",
                TransactionId = request.TransactionId,
                Status = "completed"
            });
        }

        var result = await _paymentGatewayRepository.CapturePaymentAsync(
            request,
            _completionCancellation.CaptureToken(cancellationToken));

        if (!result.IsSuccess && !result.IsPending)
        {
            return BadRequest(new
            {
                message = string.IsNullOrWhiteSpace(result.Message)
                    ? "Unable to capture payment status."
                    : result.Message
            });
        }

        var pgResponse = JsonSerializer.Serialize(result, JsonOptions);
        _paymentBackgroundQueue.EnqueuePaymentLog(
            request.OrderId,
            pgResponse ?? string.Empty);

        var paymentCompleted = result.IsSuccess && !result.IsPending;
        var gatewayTransactionId = string.IsNullOrWhiteSpace(result.TransactionId) ? request.TransactionId : result.TransactionId;

        if (paymentCompleted)
        {
            var dbToken = _completionCancellation.DbToken();

            var parentDetails = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                request.StudentId.ToString(CultureInfo.InvariantCulture),
                dbToken);

            if (topupState is not null)
            {
                await _transactionRepository.UpdateTopupTransactionStatusAsync(
                    new TopupTransactionUpdateRequest
                    {
                        TransactionPkId = topupState.TransactionPkId,
                        GatewayTransactionId = gatewayTransactionId,
                        StatusId = (int)TransactionStatusEnum.Success,
                        IsTransactionCompleted = true,
                        Remarks = string.IsNullOrWhiteSpace(result.Message) ? "Topup completed." : result.Message,
                        UpdatedBy = parentDetails?.GuardianId
                    },
                    dbToken);
            }

            await _transactionRepository.UpdatePendingAndTopupTransactionAsync(
                new UpdatePendingTransactionRequest
                {
                    CustomerID = parentDetails?.CustomerId ?? string.Empty,
                    Loaded = "1",
                    Creby = parentDetails?.Email ?? string.Empty,
                    PaymentDetails = gatewayTransactionId,
                    Remarks = request.OrderId
                },
                new UpdateTopupTransactionRequest
                {
                    CustomerID = parentDetails?.CustomerId ?? string.Empty,
                    Remarks = request.OrderId
                },
                dbToken);

            var topupAmount = topupState?.Amount ?? 0m;
            if (parentDetails is not null)
            {
                await _emailNotificationService.QueueTopupSuccessAsync(
                    request.StudentId,
                    parentDetails.GuardianId,
                    parentDetails.Email,
                    parentDetails.GuardianName,
                    request.OrderId,
                    gatewayTransactionId,
                    topupAmount,
                    dbToken);
            }
        }

        return Ok(new PaymentCaptureResult
        {
            IsSuccess = paymentCompleted,
            IsPending = result.IsPending,
            Message = string.IsNullOrWhiteSpace(result.Message)
                ? (paymentCompleted ? "Topup completed." : "Payment is still processing.")
                : result.Message,
            TransactionId = gatewayTransactionId,
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
