using System.Globalization;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Orders.Summaries;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.History;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Transaction;

namespace ETCS.Shared.Application.History;

public sealed class GuardianHistoryService : IGuardianHistoryService
{
    private readonly ITransactionRepository _transactionRepository;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IHistoryDetailRepository _historyDetailRepository;
    private readonly OrderPaymentSummaryBuilder _summaryBuilder;

    public GuardianHistoryService(
        ITransactionRepository transactionRepository,
        IMealOrderRepository mealOrderRepository,
        IHistoryDetailRepository historyDetailRepository,
        OrderPaymentSummaryBuilder summaryBuilder)
    {
        _transactionRepository = transactionRepository;
        _mealOrderRepository = mealOrderRepository;
        _historyDetailRepository = historyDetailRepository;
        _summaryBuilder = summaryBuilder;
    }

    public GuardianHistoryListResponse MapListResponse(TransactionHistoryResponse response) =>
        new()
        {
            Page = response.Page,
            PageSize = response.PageSize,
            TotalCount = response.TotalCount,
            Items = response.Items.Select(MapListItem).ToList()
        };

    public GuardianHistoryListItemDto MapListItem(TransactionHistoryItemDto item)
    {
        var status = HistoryStatusHelper.Resolve(item.StatusId, item.IsTransactionCompleted);
        var isTopup = string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase);
        var isPos = HistoryConstants.IsPosAccessLogType(item.AccessLogTransactionType);
        var isLegacyMeal = item.AccessLogTransactionType == HistoryConstants.AccessLogMealOrderType
            && item.CreatedOn.Date < HistoryConstants.LegacyCutoffDate
            && string.IsNullOrWhiteSpace(item.OrderId);
        var hasOrderDetail = !isTopup && !string.IsNullOrWhiteSpace(item.OrderId);
        var hasPosDetail = isPos && item.AccessLogId > 0;
        var hasLegacyMealDetail = isLegacyMeal && item.AccessLogId > 0;
        var hasDetail = isTopup
            ? item.Id > 0
            : hasPosDetail || hasLegacyMealDetail || hasOrderDetail;

        var detailKind = !hasDetail
            ? GuardianHistoryDetailKinds.None
            : isTopup
                ? GuardianHistoryDetailKinds.Topup
                : hasPosDetail || hasLegacyMealDetail
                    ? GuardianHistoryDetailKinds.AccessLog
                    : GuardianHistoryDetailKinds.Order;

        return new GuardianHistoryListItemDto
        {
            Id = item.Id,
            AccessLogId = item.AccessLogId,
            Aid = hasPosDetail || hasLegacyMealDetail ? item.AccessLogId : null,
            HasMealTransaction = item.HasMealTransaction,
            GuardianId = item.GuardianId,
            StudentId = item.StudentId,
            StudentName = string.IsNullOrWhiteSpace(item.StudentName) ? string.Empty : item.StudentName.Trim(),
            TransactionType = item.TransactionType,
            OrderTypeId = item.OrderTypeId,
            AccessLogTransactionType = item.AccessLogTransactionType,
            TypeLabel = ResolveTypeLabel(item),
            OrderId = item.OrderId,
            GatewayTransactionId = item.GatewayTransactionId,
            Amount = item.Amount,
            IsCredit = isTopup,
            StatusLabel = status.Label,
            IsCompleted = status.IsCompleted,
            IsPending = status.IsPending,
            IsTransactionCompleted = item.IsTransactionCompleted,
            StatusId = item.StatusId,
            Remarks = item.Remarks,
            CreatedOn = item.CreatedOn,
            UpdatedOn = item.UpdatedOn,
            PaymentMethod = item.PaymentMethod,
            HasDetail = hasDetail,
            DetailKind = detailKind,
            TopupTransactionId = isTopup && item.Id > 0 ? item.Id : null
        };
    }

    public async Task<GuardianHistoryDetailResponse?> GetOrderDetailAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, orderId, cancellationToken);
        if (order is null)
        {
            return null;
        }

        var effectiveOrderTypeId = await ResolveEffectiveOrderTypeIdAsync(
            guardianId,
            order.OrderTypeId,
            order.GatewayTransactionId,
            cancellationToken);

        var status = HistoryStatusHelper.ResolveCanonical(
            order.OrderStatusId,
            order.TransactionStatusId,
            order.IsPaid,
            order.IsTransactionCompleted);

        AlaCarteSummaryViewModel? alaCarteSummary = null;
        MealComboSummaryViewModel? comboSummary = null;

        if (effectiveOrderTypeId == (int)TransactionTypeEnum.A_La_Carte)
        {
            alaCarteSummary = await _summaryBuilder.BuildAlaCarteSummaryFromOrderAsync(
                guardianId,
                orderId,
                cancellationToken);
        }
        else if (effectiveOrderTypeId == (int)TransactionTypeEnum.MealOrder)
        {
            comboSummary = await _summaryBuilder.BuildComboSummaryFromOrderAsync(
                guardianId,
                orderId,
                cancellationToken);
        }

        return new GuardianHistoryDetailResponse
        {
            DetailKind = GuardianHistoryDetailKinds.Order,
            Order = new GuardianHistoryOrderDetailDto
            {
                IsSuccess = status.IsCompleted || order.IsPaid,
                IsPending = status.IsPending,
                StatusLabel = status.Label,
                Message = BuildOrderStatusMessage(status.Label, order.IsPaid),
                OrderId = order.OrderId,
                CreatedOn = order.CreatedOn,
                OrderTypeId = effectiveOrderTypeId,
                AlaCarteSummary = alaCarteSummary,
                ComboSummary = comboSummary
            }
        };
    }

    public async Task<GuardianHistoryDetailResponse?> GetAccessLogDetailAsync(
        int guardianId,
        long aid,
        CancellationToken cancellationToken)
    {
        var header = await _historyDetailRepository.GetAccessLogHeaderAsync(guardianId, aid, cancellationToken);
        if (header is null)
        {
            return null;
        }

        IReadOnlyList<HistoryLineItemDto> lines;
        string typeLabel;

        if (HistoryConstants.IsPosAccessLogType(header.TransactionType))
        {
            lines = await _historyDetailRepository.GetPosPurchaseDetailAsync(header, cancellationToken);
            typeLabel = HistoryConstants.ResolvePosTypeLabel(header.TransactionType);
        }
        else if (header.TransactionType == HistoryConstants.AccessLogMealOrderType
                 && header.LogDateTimeServer.Date < HistoryConstants.LegacyCutoffDate)
        {
            lines = await _historyDetailRepository.GetLegacyMealOrderDetailAsync(header, cancellationToken);
            typeLabel = "Meal Combo";
        }
        else
        {
            return null;
        }

        return new GuardianHistoryDetailResponse
        {
            DetailKind = GuardianHistoryDetailKinds.AccessLog,
            AccessLog = new GuardianHistoryAccessLogDetailDto
            {
                StatusLabel = "Completed",
                TypeLabel = typeLabel,
                StudentName = header.StudentName,
                Reference = header.TransactionId,
                TotalAmount = header.Amount,
                CreatedOn = header.LogDateTimeServer,
                Lines = lines
                    .Select(line => new GuardianHistoryLineItemDto
                    {
                        ItemName = line.ItemName,
                        SkuCode = line.SkuCode,
                        Amount = line.Amount,
                        DeliveryDate = line.DeliveryDate
                    })
                    .ToList()
            }
        };
    }

    public async Task<GuardianHistoryDetailResponse?> GetTopupDetailAsync(
        int guardianId,
        int transactionId,
        CancellationToken cancellationToken)
    {
        if (transactionId <= 0)
        {
            return null;
        }

        var transaction = await _transactionRepository.GetGuardianTransactionByIdAsync(
            guardianId,
            transactionId,
            cancellationToken);

        if (transaction is null
            || !string.Equals(transaction.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var status = HistoryStatusHelper.Resolve(transaction.StatusId, transaction.IsTransactionCompleted);
        return new GuardianHistoryDetailResponse
        {
            DetailKind = GuardianHistoryDetailKinds.Topup,
            Topup = new GuardianHistoryTopupDetailDto
            {
                Id = transaction.Id,
                StudentName = transaction.StudentName?.Trim() ?? string.Empty,
                Amount = transaction.Amount,
                StatusLabel = status.Label,
                IsCompleted = status.IsCompleted,
                IsPending = status.IsPending,
                Reference = string.IsNullOrWhiteSpace(transaction.OrderId)
                    ? transaction.Id.ToString(CultureInfo.InvariantCulture)
                    : transaction.OrderId,
                GatewayTransactionId = transaction.GatewayTransactionId?.Trim() ?? string.Empty,
                Remarks = transaction.Remarks?.Trim() ?? string.Empty,
                CreatedOn = transaction.CreatedOn
            }
        };
    }

    private async Task<int> ResolveEffectiveOrderTypeIdAsync(
        int guardianId,
        int orderTypeId,
        string gatewayTransactionId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return orderTypeId;
        }

        var accessLogType = await _historyDetailRepository.GetAccessLogTransactionTypeByGatewayTransactionAsync(
            guardianId,
            gatewayTransactionId,
            cancellationToken);

        if (accessLogType == HistoryConstants.AccessLogMealOrderType
            && orderTypeId == (int)TransactionTypeEnum.A_La_Carte)
        {
            return (int)TransactionTypeEnum.MealOrder;
        }

        return orderTypeId;
    }

    private static string ResolveTypeLabel(TransactionHistoryItemDto item)
    {
        if (string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
        {
            return "Top-up";
        }

        if (item.OrderTypeId == (int)TransactionTypeEnum.POS)
        {
            return HistoryConstants.ResolvePosTypeLabel(item.AccessLogTransactionType);
        }

        var orderLabel = item.OrderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => "Ala-Carte",
            (int)TransactionTypeEnum.MealOrder => "Meal Combo",
            _ => null
        };

        if (!string.IsNullOrWhiteSpace(orderLabel))
        {
            return orderLabel;
        }

        if (!string.IsNullOrWhiteSpace(item.Remarks))
        {
            return item.Remarks.Trim();
        }

        return "Transaction";
    }

    private static string BuildOrderStatusMessage(string statusLabel, bool isPaid)
    {
        if (isPaid || string.Equals(statusLabel, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Order details are shown below.";
        }

        if (string.Equals(statusLabel, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "This order payment is still processing.";
        }

        return "This order was not completed successfully.";
    }
}
