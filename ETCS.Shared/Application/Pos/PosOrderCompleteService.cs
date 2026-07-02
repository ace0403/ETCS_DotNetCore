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

        await _posOrderRepository.MarkPaymentCompletedAsync(
            request.OrderId,
            request.IbonusTransactionId,
            (int)TransactionStatusEnum.Success,
            (int)TransactionStatusEnum.Success,
            cancellationToken);

        var (accessLogTypeId, orderDescription) = OrderAccessLogResolver.Resolve(paymentState.OrderTypeId);
        var terminalCode = string.IsNullOrWhiteSpace(request.TerminalCode) ? "777" : request.TerminalCode.Trim();
        var companyCode = string.IsNullOrWhiteSpace(_posOptions.DefaultCompanyCode) ? "240" : _posOptions.DefaultCompanyCode;

        var accessLogId = await _mainOrderRepository.ApplySuccessfulOrderAsync(
            customerId,
            request.OrderId,
            request.IbonusTransactionId,
            paymentState.Total,
            orderDescription,
            (short)accessLogTypeId,
            _orderFlowOptions.AccessLogDescription,
            terminalCode,
            companyCode,
            cancellationToken);

        await _posOrderRepository.AttachAccessLogIdAsync(request.OrderId, accessLogId, cancellationToken);

        return new PosOrderCompleteResponse
        {
            IsSuccess = true,
            Message = "POS order completed successfully.",
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

    private static PosOrderCompleteResponse Fail(string message) =>
        new() { IsSuccess = false, Message = message };
}
