using ETCS.Shared.Application.Orders;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ETCS.API.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Route("api/v{version:apiVersion}/[controller]")]
[Route("api/[controller]")]
[Authorize]
public sealed class OrderController : ControllerBase
{
    private readonly IOrderInitiateService _orderInitiateService;
    private readonly IOrderPaymentCompleteService _orderPaymentCompleteService;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IStudentRepository _studentRepository;

    public OrderController(
        IOrderInitiateService orderInitiateService,
        IOrderPaymentCompleteService orderPaymentCompleteService,
        IMealOrderRepository mealOrderRepository,
        IStudentRepository studentRepository)
    {
        _orderInitiateService = orderInitiateService;
        _orderPaymentCompleteService = orderPaymentCompleteService;
        _mealOrderRepository = mealOrderRepository;
        _studentRepository = studentRepository;
    }

    [HttpPost("initiate")]
    public async Task<IActionResult> Initiate(
        [FromBody] OrderInitiateRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedGuardianId(out var guardianId))
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

        var scopedRequest = new OrderInitiateRequest
        {
            StudentId = request.StudentId,
            GuardianId = guardianId,
            OrderId = request.OrderId,
            OrderStatusId = (int)TransactionStatusEnum.Pending,
            OrderTypeId = request.OrderTypeId,
            Total = request.Total,
            Notes = request.Notes,
            MealList = request.MealList
        };

        var result = await _orderInitiateService.InitiateAsync(scopedRequest, cancellationToken);
        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete(
        [FromBody] OrderCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedGuardianId(out var guardianId))
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

        if (string.IsNullOrWhiteSpace(request.TransactionId))
        {
            return BadRequest(new { message = "TransactionId is required." });
        }

        var scopedRequest = new OrderCompleteRequest
        {
            StudentId = request.StudentId,
            GuardianId = guardianId,
            OrderId = request.OrderId,
            TransactionId = request.TransactionId
        };

        var result = await _orderPaymentCompleteService.CompleteAsync(scopedRequest, cancellationToken);
        if (result.IsPending)
        {
            return Ok(result);
        }

        if (!result.IsSuccess)
        {
            return BadRequest(new { message = result.Message });
        }

        return Ok(result);
    }

    [HttpGet("list")]
    public async Task<IActionResult> GetOrderList(
        [FromQuery] int? studentId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetAuthenticatedGuardianId(out var guardianId))
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

        var result = await _mealOrderRepository.GetOrderListAsync(guardianId, studentId, page, pageSize, cancellationToken);

        var studentMap = await GetStudentNameMapAsync(guardianId, cancellationToken);
        var enrichedItems = result.Items
            .Select(item => new OrderListItemDto
            {
                Id = item.Id,
                OrderId = item.OrderId,
                StudentId = item.StudentId,
                StudentName = studentMap.TryGetValue(item.StudentId, out var name) ? name : string.Empty,
                GuardianId = item.GuardianId,
                Total = item.Total,
                TotalItems = item.TotalItems,
                OrderStatusId = item.OrderStatusId,
                IsPaid = item.IsPaid,
                OrderDate = item.OrderDate,
                CreatedOn = item.CreatedOn
            })
            .ToList();

        return Ok(new OrderListResponse
        {
            Page = result.Page,
            PageSize = result.PageSize,
            TotalCount = result.TotalCount,
            Items = enrichedItems
        });
    }

    [HttpGet("detail/{orderId}")]
    public async Task<IActionResult> GetOrderDetail(
        [FromRoute] string orderId,
        CancellationToken cancellationToken)
    {
        if (!TryGetAuthenticatedGuardianId(out var guardianId))
        {
            return Unauthorized(new { message = "Guardian claim is missing in token." });
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return BadRequest(new { message = "OrderId is required." });
        }

        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, orderId.Trim(), cancellationToken);
        if (order is null)
        {
            return NotFound(new { message = "Order not found." });
        }

        var studentMap = await GetStudentNameMapAsync(guardianId, cancellationToken);
        var studentName = studentMap.TryGetValue(order.StudentId, out var name) ? name : string.Empty;

        var enrichedOrder = new OrderDetailDto
        {
            Id = order.Id,
            OrderId = order.OrderId,
            StudentId = order.StudentId,
            StudentName = studentName,
            GuardianId = order.GuardianId,
            OrderTypeId = order.OrderTypeId,
            SubTotal = order.SubTotal,
            TaxAmount = order.TaxAmount,
            Total = order.Total,
            TotalItems = order.TotalItems,
            OrderStatusId = order.OrderStatusId,
            IsPaid = order.IsPaid,
            Notes = order.Notes,
            OrderDate = order.OrderDate,
            CreatedOn = order.CreatedOn,
            LineItems = order.LineItems
        };

        return Ok(enrichedOrder);
    }

    private bool TryGetAuthenticatedGuardianId(out int guardianId)
    {
        guardianId = 0;
        var raw = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("nameid")
            ?? User.FindFirstValue("guardianId")
            ?? User.FindFirstValue("GuardianId");

        return int.TryParse(raw, out guardianId) && guardianId > 0;
    }

    private async Task<Dictionary<int, string>> GetStudentNameMapAsync(int guardianId, CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, customerId: null, cancellationToken);
        return students
            .GroupBy(s => Convert.ToInt32(s.UserId))
            .ToDictionary(g => g.Key, g => g.First().Name?.Trim() ?? string.Empty);
    }
}
