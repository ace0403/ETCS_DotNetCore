using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Web.Models;
using System.Globalization;

namespace ETCS.Web.Infrastructure.Orders;

public sealed class OrderPaymentSummaryBuilder
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IStudentRepository _studentRepository;

    public OrderPaymentSummaryBuilder(
        IMealOrderRepository mealOrderRepository,
        IMealRepository mealRepository,
        IStudentRepository studentRepository)
    {
        _mealOrderRepository = mealOrderRepository;
        _mealRepository = mealRepository;
        _studentRepository = studentRepository;
    }

    public async Task<OrderPaymentReturnViewModel> BuildReceiptAsync(
        int guardianId,
        int orderTypeId,
        string orderId,
        bool isSuccess,
        bool isPending,
        string message,
        CancellationToken cancellationToken)
    {
        AlaCarteSummaryViewModel? alaCarteSummary = null;
        MealComboSummaryViewModel? comboSummary = null;

        if (isSuccess)
        {
            if (orderTypeId == (int)TransactionTypeEnum.A_La_Carte)
            {
                alaCarteSummary = await BuildAlaCarteSummaryFromOrderAsync(guardianId, orderId, cancellationToken);
            }
            else if (orderTypeId == (int)TransactionTypeEnum.MealOrder)
            {
                comboSummary = await BuildComboSummaryFromOrderAsync(guardianId, orderId, cancellationToken);
            }
        }

        return new OrderPaymentReturnViewModel
        {
            IsSuccess = isSuccess,
            IsPending = isPending,
            Message = message,
            OrderId = orderId,
            OrderTypeId = orderTypeId,
            AlaCarteSummary = alaCarteSummary,
            ComboSummary = comboSummary
        };
    }

    public async Task<AlaCarteSummaryViewModel?> BuildAlaCarteSummaryFromOrderAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, orderId, cancellationToken);
        if (order is null || order.LineItems.Count == 0)
        {
            return null;
        }

        var studentName = await GetStudentNameAsync(guardianId, order.StudentId, cancellationToken);
        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(order.StudentId, cancellationToken);
        var menuLookup = new Dictionary<(int ItemId, DateTime MealDate), MealItemDto>();

        if (schoolId is > 0)
        {
            foreach (var mealDate in order.LineItems.Select(x => x.MealDate.Date).Distinct())
            {
                var menuItems = await _mealRepository.GetMealItemsForStudentAsync(
                    order.StudentId,
                    schoolId.Value,
                    mealDate,
                    cancellationToken: cancellationToken);

                foreach (var menuItem in menuItems)
                {
                    menuLookup[(menuItem.Id, mealDate)] = menuItem;
                }
            }
        }

        var summaryItems = order.LineItems
            .Select(line =>
            {
                MealItemDto? menuItem = null;
                if (line.ItemId is > 0)
                {
                    menuLookup.TryGetValue((line.ItemId.Value, line.MealDate.Date), out menuItem);
                }

                return new AlaCarteSummaryItem
                {
                    Id = line.ItemId ?? line.Id,
                    SelectionId = Guid.Empty,
                    ItemName = string.IsNullOrWhiteSpace(line.ItemName)
                        ? menuItem?.ItemName ?? "Meal item"
                        : line.ItemName,
                    MealTypeName = menuItem?.MealTypeName ?? string.Empty,
                    Price = line.ItemPrice,
                    MealDate = line.MealDate,
                    ImageName = menuItem?.ImageName
                };
            })
            .ToList();

        return new AlaCarteSummaryViewModel
        {
            OrderAmount = order.Total,
            StudentName = studentName,
            SelectedMeals = summaryItems
        };
    }

    public async Task<MealComboSummaryViewModel?> BuildComboSummaryFromOrderAsync(
        int guardianId,
        string orderId,
        CancellationToken cancellationToken)
    {
        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, orderId, cancellationToken);
        if (order is null || order.LineItems.Count == 0)
        {
            return null;
        }

        var studentName = await GetStudentNameAsync(guardianId, order.StudentId, cancellationToken);
        var schoolId = await _studentRepository.GetStudentSchoolIdAsync(order.StudentId, cancellationToken);
        var packageLookup = new Dictionary<(int PackageId, DateTime MealDate), MealPackageDto>();
        var itemLookup = new Dictionary<(int ItemId, DateTime MealDate), MealItemDto>();

        if (schoolId is > 0)
        {
            foreach (var mealDate in order.LineItems.Select(x => x.MealDate.Date).Distinct())
            {
                var packages = await _mealRepository.GetMealPackagesForStudentAsync(
                    order.StudentId,
                    schoolId.Value,
                    mealDate,
                    cancellationToken: cancellationToken);

                foreach (var package in packages)
                {
                    packageLookup[(package.Id, mealDate)] = package;
                }

                var menuItems = await _mealRepository.GetMealItemsForStudentAsync(
                    order.StudentId,
                    schoolId.Value,
                    mealDate,
                    cancellationToken: cancellationToken);

                foreach (var menuItem in menuItems)
                {
                    itemLookup[(menuItem.Id, mealDate)] = menuItem;
                }
            }
        }

        var summaryItems = order.LineItems
            .Select(line =>
            {
                if (line.ItemId is > 0)
                {
                    itemLookup.TryGetValue((line.ItemId.Value, line.MealDate.Date), out var menuItem);
                    return new MealComboSummaryItem
                    {
                        Id = line.ItemId.Value,
                        SelectionId = Guid.Empty,
                        IsAddon = true,
                        ItemName = string.IsNullOrWhiteSpace(line.ItemName)
                            ? menuItem?.ItemName ?? "Add-on"
                            : line.ItemName,
                        MealTypeName = menuItem?.MealTypeName ?? string.Empty,
                        MealSessionName = menuItem?.MealSessionName ?? string.Empty,
                        Detail = menuItem?.Detail,
                        Price = line.ItemPrice,
                        MealDate = line.MealDate,
                        ImageName = menuItem?.ImageName
                    };
                }

                MealPackageDto? package = null;
                if (line.PackageId is > 0)
                {
                    packageLookup.TryGetValue((line.PackageId.Value, line.MealDate.Date), out package);
                }

                return new MealComboSummaryItem
                {
                    Id = line.PackageId ?? line.Id,
                    SelectionId = Guid.Empty,
                    IsAddon = false,
                    PackageName = string.IsNullOrWhiteSpace(line.ItemName)
                        ? package?.PackageName ?? "Meal combo"
                        : line.ItemName,
                    ItemsName = package?.ItemsName ?? string.Empty,
                    MealTypeName = package?.MealTypeName ?? string.Empty,
                    MealSessionName = package?.MealSessionName ?? string.Empty,
                    Detail = package?.Detail,
                    Price = line.ItemPrice,
                    MealDate = line.MealDate,
                    ImageName = package?.ImageName
                };
            })
            .ToList();

        return new MealComboSummaryViewModel
        {
            OrderAmount = order.Total,
            StudentName = studentName,
            SelectedLines = summaryItems
        };
    }

    private async Task<string> GetStudentNameAsync(int guardianId, int studentId, CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, customerId: null, cancellationToken);
        return students
            .Where(s => Convert.ToInt32(s.UserId) == studentId)
            .Select(s => string.IsNullOrWhiteSpace(s.Name) ? (s.UserName ?? "Student") : s.Name.Trim())
            .FirstOrDefault() ?? string.Empty;
    }

    public static string? ResolvePaymentReturnOrderId(string? queryOrderId, ETCS.PaymentGateway.Models.ComtrustCallbackRequest? callback)
    {
        if (!string.IsNullOrWhiteSpace(queryOrderId))
        {
            return queryOrderId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(callback?.OrderID))
        {
            return callback.OrderID.Trim();
        }

        return null;
    }

    public static DateTime ParseMealDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed.Date
            : default;
    }
}
