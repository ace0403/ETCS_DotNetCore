using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Pos;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ETCS.Shared.Application.Pos;

public sealed class PosOrderInitiateService : IPosOrderInitiateService
{
    private readonly IPOSOrderRepository _posOrderRepository;
    private readonly IPosSpendRepository _posSpendRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly PosOptions _posOptions;

    public PosOrderInitiateService(
        IPOSOrderRepository posOrderRepository,
        IPosSpendRepository posSpendRepository,
        IStudentRepository studentRepository,
        IOptions<PosOptions> posOptions)
    {
        _posOrderRepository = posOrderRepository;
        _posSpendRepository = posSpendRepository;
        _studentRepository = studentRepository;
        _posOptions = posOptions.Value;
    }

    public async Task<PosOrderInitiateResponse> InitiateAsync(
        PosOrderInitiateRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StudentId <= 0)
        {
            return Fail("StudentId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.TerminalCode))
        {
            return Fail("TerminalCode is required.");
        }

        if (request.MealList is null || request.MealList.Count == 0)
        {
            return Fail("At least one cart item is required.");
        }

        var lineTotal = request.MealList.Sum(x => x.Total);
        if (Math.Abs(lineTotal - request.Total) > 0.01m)
        {
            return Fail("Total does not match sum of line item totals.");
        }

        var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
            request.StudentId.ToString(CultureInfo.InvariantCulture),
            cancellationToken);
        if (guardianDetail is null || string.IsNullOrWhiteSpace(guardianDetail.CustomerId))
        {
            return Fail("Unable to resolve customer profile for this student.");
        }

        var spendInfo = await _posSpendRepository.GetSpendInfoByCustomerIdAsync(
            guardianDetail.CustomerId,
            _posOptions.OrderTypeId,
            cancellationToken);
        if (spendInfo is not null)
        {
            if (spendInfo.IsDailyLimitExceeded)
            {
                return Fail("Daily spending limit exceeded.");
            }

            if (spendInfo.IsWeeklyLimitExceeded)
            {
                return Fail("Weekly spending limit exceeded.");
            }

            if (spendInfo.DailySpendLimit > 0 && spendInfo.DailySpent + request.Total > spendInfo.DailySpendLimit)
            {
                return Fail("This purchase would exceed the daily spending limit.");
            }

            if (spendInfo.WeeklySpendLimit > 0 && spendInfo.WeeklySpent + request.Total > spendInfo.WeeklySpendLimit)
            {
                return Fail("This purchase would exceed the weekly spending limit.");
            }
        }

        var orderId = string.IsNullOrWhiteSpace(request.OrderId)
            ? OrderIdGenerator.GenerateForStudent(request.StudentId)
            : request.OrderId.Trim();

        if (await _posOrderRepository.OrderIdExistsAsync(orderId, cancellationToken))
        {
            return Fail("Order ID already exists.");
        }

        var notes = string.IsNullOrWhiteSpace(request.Notes)
            ? "POS Order | Terminal " + request.TerminalCode.Trim()
            : request.Notes.Trim() + " | Terminal " + request.TerminalCode.Trim();

        var mealTransactionId = await _posOrderRepository.CreatePendingOrderAsync(
            new OrderInitiateRequest
            {
                StudentId = request.StudentId,
                GuardianId = guardianDetail.GuardianId,
                OrderId = orderId,
                OrderStatusId = (int)TransactionStatusEnum.Pending,
                OrderTypeId = _posOptions.OrderTypeId,
                Total = request.Total,
                Notes = notes,
                MealList = request.MealList
            },
            (int)TransactionStatusEnum.Initiated,
            cancellationToken);

        return new PosOrderInitiateResponse
        {
            IsSuccess = true,
            Message = "POS order initiated successfully.",
            OrderId = orderId,
            StudentId = request.StudentId,
            GuardianId = guardianDetail.GuardianId,
            Total = request.Total,
            MealTransactionId = mealTransactionId
        };
    }

    private static PosOrderInitiateResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
