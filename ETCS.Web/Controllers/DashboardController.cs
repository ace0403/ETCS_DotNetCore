using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Notifications;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace ETCS.Web.Controllers;

[Authorize]
public sealed class DashboardController : Controller
{
    private const int RecentActivityCount = 5;
    private const int NotificationCount = 3;
    private const int HistoryWindowPageSize = 200;
    private const int ChartMonthCount = 7;

    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly IGuardianNotificationRepository _notificationRepository;

    public DashboardController(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository,
        IGuardianNotificationRepository notificationRepository)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
        _notificationRepository = notificationRepository;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var fromDate = DateTime.Today.AddMonths(-(ChartMonthCount - 1));
        fromDate = new DateTime(fromDate.Year, fromDate.Month, 1);

        var studentsTask = _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var historyTask = _transactionRepository.GetTransactionHistoryAsync(
            studentId: null,
            guardianId: guardianId,
            type: "all",
            fromDate: fromDate,
            toDate: DateTime.Today,
            page: 1,
            pageSize: HistoryWindowPageSize,
            cancellationToken);

        await Task.WhenAll(studentsTask, historyTask);

        var students = await studentsTask;
        var history = await historyTask;
        var items = history.Items.ToList();

        var children = students
            .Select(s => new DashboardChildItem
            {
                StudentId = Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture),
                Name = s.Name?.Trim() ?? string.Empty,
                Balance = s.Balprepaid ?? 0m,
                CardId = s.Cardid?.Trim() ?? string.Empty
            })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var today = DateTime.Today;
        var monthStart = new DateTime(today.Year, today.Month, 1);

        var orderItems = items
            .Where(i => !string.Equals(i.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var monthlySpending = orderItems
            .Where(i => i.CreatedOn.Date >= monthStart
                        && HistoryStatusHelper.CountsTowardSpend(i.StatusId, i.IsTransactionCompleted))
            .Sum(i => i.Amount);

        var todayOrdersCount = orderItems.Count(i => i.CreatedOn.Date == today);

        var pendingOrdersCount = items.Count(i =>
        {
            var status = HistoryStatusHelper.Resolve(i.StatusId, i.IsTransactionCompleted);
            return status.IsPending;
        });

        var activePlansCount = orderItems.Count(i =>
        {
            if (i.OrderTypeId != (int)TransactionTypeEnum.MealOrder)
            {
                return false;
            }

            var status = HistoryStatusHelper.Resolve(i.StatusId, i.IsTransactionCompleted);
            return status.IsPending || status.IsCompleted;
        });

        var monthlySpendSeries = BuildMonthlySpendSeries(orderItems, today);
        var monthlySpendingChangePercent = ComputeMonthOverMonthChange(monthlySpendSeries);

        var previousMonthTopups = items
            .Where(i => string.Equals(i.TransactionType, "topup", StringComparison.OrdinalIgnoreCase)
                        && HistoryStatusHelper.CountsTowardSpend(i.StatusId, i.IsTransactionCompleted)
                        && i.CreatedOn >= monthStart.AddMonths(-1)
                        && i.CreatedOn < monthStart)
            .Sum(i => i.Amount);
        var currentMonthTopups = items
            .Where(i => string.Equals(i.TransactionType, "topup", StringComparison.OrdinalIgnoreCase)
                        && HistoryStatusHelper.CountsTowardSpend(i.StatusId, i.IsTransactionCompleted)
                        && i.CreatedOn >= monthStart)
            .Sum(i => i.Amount);
        var walletBalanceChangePercent = ComputePercentChange(previousMonthTopups, currentMonthTopups);

        var orderedItems = items.OrderByDescending(i => i.CreatedOn).ToList();
        var notifications = await _notificationRepository.GetByGuardianAsync(
            guardianId,
            NotificationCount,
            unreadOnly: false,
            cancellationToken);

        var model = new DashboardPageViewModel
        {
            GuardianDisplayName = User.GetDisplayName()?.Trim() ?? string.Empty,
            TotalBalance = children.Sum(c => c.Balance),
            WalletBalanceChangePercent = walletBalanceChangePercent,
            MonthlySpending = monthlySpending,
            MonthlySpendingChangePercent = monthlySpendingChangePercent,
            TodayOrdersCount = todayOrdersCount,
            ActivePlansCount = activePlansCount,
            PendingOrdersCount = pendingOrdersCount,
            Children = children,
            RecentActivity = orderedItems
                .Take(RecentActivityCount)
                .Select(MapActivityItem)
                .ToList(),
            TodaysMeals = BuildTodaysMeals(orderItems, today),
            Notifications = notifications.Select(MapNotification).ToList(),
            MonthlySpendSeries = monthlySpendSeries,
            CategoryBreakdown = BuildCategoryBreakdown(items)
        };

        return View(model);
    }

    private static decimal? ComputeMonthOverMonthChange(IReadOnlyList<DashboardChartPoint> series)
    {
        if (series.Count < 2)
        {
            return null;
        }

        return ComputePercentChange(series[^2].Value, series[^1].Value);
    }

    private static decimal? ComputePercentChange(decimal previous, decimal current)
    {
        if (previous == 0m)
        {
            return current == 0m ? null : 100m;
        }

        return Math.Round((current - previous) / previous * 100m, 0, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<DashboardChartPoint> BuildMonthlySpendSeries(
        IReadOnlyList<TransactionHistoryItemDto> orderItems,
        DateTime today)
    {
        var points = new List<DashboardChartPoint>(ChartMonthCount);
        for (var offset = ChartMonthCount - 1; offset >= 0; offset--)
        {
            var monthDate = new DateTime(today.Year, today.Month, 1).AddMonths(-offset);
            var monthEnd = monthDate.AddMonths(1);
            var total = orderItems
                .Where(i => i.CreatedOn >= monthDate
                            && i.CreatedOn < monthEnd
                            && HistoryStatusHelper.CountsTowardSpend(i.StatusId, i.IsTransactionCompleted))
                .Sum(i => i.Amount);

            points.Add(new DashboardChartPoint
            {
                Label = monthDate.ToString("MMM", CultureInfo.InvariantCulture),
                Value = total
            });
        }

        return points;
    }

    private static IReadOnlyList<DashboardChartPoint> BuildCategoryBreakdown(
        IReadOnlyList<TransactionHistoryItemDto> items)
    {
        decimal alaCarte = 0m;
        decimal mealCombo = 0m;
        decimal topup = 0m;
        decimal other = 0m;

        foreach (var item in items)
        {
            if (!HistoryStatusHelper.CountsTowardSpend(item.StatusId, item.IsTransactionCompleted))
            {
                continue;
            }

            if (string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
            {
                topup += item.Amount;
                continue;
            }

            switch (item.OrderTypeId)
            {
                case (int)TransactionTypeEnum.A_La_Carte:
                    alaCarte += item.Amount;
                    break;
                case (int)TransactionTypeEnum.MealOrder:
                    mealCombo += item.Amount;
                    break;
                default:
                    other += item.Amount;
                    break;
            }
        }

        var points = new List<DashboardChartPoint>();
        if (alaCarte > 0) points.Add(new DashboardChartPoint { Label = "Ala-Carte", Value = alaCarte });
        if (mealCombo > 0) points.Add(new DashboardChartPoint { Label = "Meal Combo", Value = mealCombo });
        if (topup > 0) points.Add(new DashboardChartPoint { Label = "Top-up", Value = topup });
        if (other > 0) points.Add(new DashboardChartPoint { Label = "Other", Value = other });
        return points;
    }

    private IReadOnlyList<DashboardTodayMealItem> BuildTodaysMeals(
        IReadOnlyList<TransactionHistoryItemDto> orderItems,
        DateTime today)
    {
        return orderItems
            .Where(i => i.CreatedOn.Date == today)
            .OrderByDescending(i => i.CreatedOn)
            .Take(8)
            .Select(i =>
            {
                var status = HistoryStatusHelper.Resolve(i.StatusId, i.IsTransactionCompleted);
                var (label, css) = MapTodayMealStatus(status);
                return new DashboardTodayMealItem
                {
                    StudentName = string.IsNullOrWhiteSpace(i.StudentName) ? "—" : i.StudentName.Trim(),
                    MealLabel = ResolveMealLabel(i),
                    StatusLabel = label,
                    StatusCss = css,
                    DetailUrl = Url.Action("Detail", "History", new { orderId = i.OrderId }) ?? "#"
                };
            })
            .ToList();
    }

    private static (string Label, string Css) MapTodayMealStatus(
        (string Label, string Css, bool IsPending, bool IsCompleted, bool IsFailed) status)
    {
        if (status.IsFailed)
        {
            return string.Equals(status.Label, "Cancelled", StringComparison.OrdinalIgnoreCase)
                ? ("cancelled", "is-failed")
                : ("failed", "is-failed");
        }

        if (status.IsPending)
        {
            return ("pending", "is-pending");
        }

        return ("confirmed", "is-confirmed");
    }

    private NotificationListItemViewModel MapNotification(GuardianNotificationDto n)
    {
        var (icon, tone) = n.Type switch
        {
            GuardianNotificationTypes.TopupSuccess => ("wallet.svg", "tone-topup"),
            GuardianNotificationTypes.OrderConfirmed => ("shopping.svg", "tone-order"),
            GuardianNotificationTypes.OrderPending => ("clock.svg", "tone-pending"),
            GuardianNotificationTypes.Announcement => ("notification.svg", "tone-announce"),
            _ => ("notification.svg", "tone-system")
        };

        return new()
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            Icon = icon,
            ToneCss = tone,
            IsRead = n.IsRead,
            CreatedOn = n.CreatedOn,
            RelativeTime = FormatRelativeTime(n.CreatedOn),
            DetailUrl = Url.Action("Open", "Notifications", new { id = n.Id }) ?? "#"
        };
    }

    private DashboardActivityItem MapActivityItem(TransactionHistoryItemDto item)
    {
        var isTopup = string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase);
        var studentName = string.IsNullOrWhiteSpace(item.StudentName) ? "your child" : item.StudentName.Trim();
        var detailUrl = isTopup
            ? Url.Action("TopupDetail", "History", new { id = item.Id }) ?? "#"
            : Url.Action("Detail", "History", new { orderId = item.OrderId }) ?? "#";
        var status = HistoryStatusHelper.Resolve(item.StatusId, item.IsTransactionCompleted);

        if (isTopup)
        {
            return new DashboardActivityItem
            {
                Title = $"Wallet topped up by AED {item.Amount:0.00}",
                RelativeTime = FormatRelativeTime(item.CreatedOn),
                Icon = "wallet.svg",
                Tone = "tone-topup",
                StatusLabel = status.Label,
                StatusCss = status.Css,
                DetailUrl = detailUrl
            };
        }

        if (item.OrderTypeId == (int)TransactionTypeEnum.MealOrder)
        {
            return new DashboardActivityItem
            {
                Title = $"Meal plan ordered for {studentName}",
                RelativeTime = FormatRelativeTime(item.CreatedOn),
                Icon = "plan.svg",
                Tone = "tone-combo",
                StatusLabel = status.Label,
                StatusCss = status.Css,
                DetailUrl = detailUrl
            };
        }

        return new DashboardActivityItem
        {
            Title = $"{ResolveMealLabel(item)} ordered for {studentName}",
            RelativeTime = FormatRelativeTime(item.CreatedOn),
            Icon = "shopping.svg",
            Tone = "tone-order",
            StatusLabel = status.Label,
            StatusCss = status.Css,
            DetailUrl = detailUrl
        };
    }

    private static string ResolveMealLabel(TransactionHistoryItemDto item)
    {
        return item.OrderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => "A La Carte",
            (int)TransactionTypeEnum.MealOrder => "Meal Plan",
            (int)TransactionTypeEnum.POS => "POS",
            _ => "Order"
        };
    }

    private static string FormatRelativeTime(DateTime createdOn)
    {
        var local = createdOn.Kind == DateTimeKind.Utc ? createdOn.ToLocalTime() : createdOn;
        var span = DateTime.Now - local;

        if (span.TotalMinutes < 1)
        {
            return "Just now";
        }

        if (span.TotalMinutes < 60)
        {
            var minutes = Math.Max(1, (int)span.TotalMinutes);
            return minutes == 1 ? "1 minute ago" : $"{minutes} minutes ago";
        }

        if (span.TotalHours < 24)
        {
            var hours = Math.Max(1, (int)span.TotalHours);
            return hours == 1 ? "1 hour ago" : $"{hours} hours ago";
        }

        if (span.TotalDays < 2)
        {
            return "Yesterday";
        }

        var days = (int)span.TotalDays;
        return days == 1 ? "1 day ago" : $"{days} days ago";
    }
}
