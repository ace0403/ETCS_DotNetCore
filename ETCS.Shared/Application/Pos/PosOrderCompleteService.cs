using ETCS.Shared.Application.Orders;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Pos;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Options;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace ETCS.Shared.Application.Pos;

public sealed class PosOrderCompleteService : IPosOrderCompleteService
{
    private readonly IPOSOrderRepository _posOrderRepository;
    private readonly IMainOrderRepository _mainOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly PosOptions _posOptions;
    private readonly OrderFlowOptions _orderFlowOptions;

    public PosOrderCompleteService(
        IPOSOrderRepository posOrderRepository,
        IMainOrderRepository mainOrderRepository,
        IStudentRepository studentRepository,
        IOptions<PosOptions> posOptions,
        IOptions<OrderFlowOptions> orderFlowOptions)
    {
        _posOrderRepository = posOrderRepository;
        _mainOrderRepository = mainOrderRepository;
        _studentRepository = studentRepository;
        _posOptions = posOptions.Value;
        _orderFlowOptions = orderFlowOptions.Value;
    }

    public async Task<PosOrderCompleteResponse> CompleteAsync(
        PosOrderCompleteRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.OrderId))
        {
            return Fail("OrderId is required.");
        }

        if (string.IsNullOrWhiteSpace(request.IbonusTransactionId))
        {
            return Fail("IbonusTransactionId is required.");
        }

        var paymentState = await _posOrderRepository.GetPaymentStateForCompletionAsync(request.OrderId, cancellationToken);
        if (paymentState is null)
        {
            return Fail("Order not found.");
        }

        if (paymentState.IsPaid && paymentState.AccessLogId.HasValue)
        {
            return new PosOrderCompleteResponse
            {
                IsSuccess = true,
                IsAlreadyProcessed = true,
                Message = "POS order already completed.",
                OrderId = request.OrderId,
                IbonusTransactionId = request.IbonusTransactionId,
                AccessLogId = paymentState.AccessLogId.Value
            };
        }

        var customerId = request.CustomerId.Trim();
        if (string.IsNullOrWhiteSpace(customerId))
        {
            var guardianDetail = await _studentRepository.GetGuardianBasicDetailByStudentIdAsync(
                request.StudentId.ToString(CultureInfo.InvariantCulture),
                cancellationToken);
            customerId = guardianDetail?.CustomerId ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            return Fail("Unable to resolve customer profile.");
        }

        var terminalCode = string.IsNullOrWhiteSpace(request.TerminalCode) ? "777" : request.TerminalCode.Trim();
        var companyCode = string.IsNullOrWhiteSpace(_posOptions.DefaultCompanyCode) ? "240" : _posOptions.DefaultCompanyCode;
        var alreadyMarkedSuccess = paymentState.IsPaid
            || paymentState.IsTransactionCompleted
            || paymentState.TransactionStatusId == (int)TransactionStatusEnum.Success;

        if (!alreadyMarkedSuccess)
        {
            await _posOrderRepository.MarkPaymentCompletedAsync(
                request.OrderId,
                request.IbonusTransactionId,
                (int)TransactionStatusEnum.Success,
                (int)TransactionStatusEnum.Success,
                cancellationToken);
        }

        var accessLogId = await EnsureAccessLogAttachedAsync(
            request.OrderId,
            customerId,
            request.IbonusTransactionId,
            paymentState.Total,
            paymentState.OrderTypeId,
            terminalCode,
            companyCode,
            cancellationToken);

        return new PosOrderCompleteResponse
        {
            IsSuccess = true,
            IsAlreadyProcessed = alreadyMarkedSuccess,
            Message = alreadyMarkedSuccess
                ? "POS order already completed; AccessLog linked."
                : "POS order completed successfully.",
            OrderId = request.OrderId,
            IbonusTransactionId = request.IbonusTransactionId,
            AccessLogId = accessLogId
        };
    }

    public async Task<PosOrderCompleteResponse> UndoAsync(
        PosOrderUndoRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            return Fail("CustomerId is required for undo.");
        }

        if (string.IsNullOrWhiteSpace(request.IbonusTransactionId))
        {
            return Fail("IbonusTransactionId is required for undo.");
        }

        var terminalCode = string.IsNullOrWhiteSpace(request.TerminalCode) ? "777" : request.TerminalCode.Trim();
        var companyCode = string.IsNullOrWhiteSpace(_posOptions.DefaultCompanyCode) ? "240" : _posOptions.DefaultCompanyCode;

        var accessLogId = await _mainOrderRepository.InsertAccessLogAsync(
            request.CustomerId.Trim(),
            0m,
            (short)AccessLogTypeEnum.A_La_Carte,
            "POS Undo",
            request.IbonusTransactionId,
            terminalCode,
            companyCode,
            cancellationToken);

        return new PosOrderCompleteResponse
        {
            IsSuccess = true,
            Message = "POS undo recorded.",
            OrderId = request.OrderId,
            IbonusTransactionId = request.IbonusTransactionId,
            AccessLogId = accessLogId
        };
    }

    private async Task<long> EnsureAccessLogAttachedAsync(
        string orderId,
        string customerId,
        string ibonusTransactionId,
        decimal total,
        int orderTypeId,
        string terminalCode,
        string companyCode,
        CancellationToken cancellationToken)
    {
        var existing = await _mainOrderRepository.FindAccessLogIdByGatewayTransactionAsync(
            customerId,
            ibonusTransactionId,
            cancellationToken);

        long accessLogId;
        if (existing is > 0)
        {
            accessLogId = existing.Value;
        }
        else
        {
            var (accessLogTypeId, orderDescription) = OrderAccessLogResolver.Resolve(orderTypeId);
            accessLogId = await _mainOrderRepository.ApplySuccessfulOrderAsync(
                customerId,
                orderId,
                ibonusTransactionId,
                total,
                orderDescription,
                (short)accessLogTypeId,
                _orderFlowOptions.AccessLogDescription,
                terminalCode,
                companyCode,
                cancellationToken);
        }

        await _posOrderRepository.AttachAccessLogIdAsync(orderId, accessLogId, cancellationToken);
        return accessLogId;
    }

    private static PosOrderCompleteResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
