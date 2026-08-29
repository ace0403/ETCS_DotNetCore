using System.Globalization;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

[Authorize]
public sealed class OrderCalendarController : Controller
{
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly ISchoolCalendarService _schoolCalendar;

    public OrderCalendarController(
        IMealOrderRepository mealOrderRepository,
        IStudentRepository studentRepository,
        ISchoolCalendarService schoolCalendar)
    {
        _mealOrderRepository = mealOrderRepository;
        _studentRepository = studentRepository;
        _schoolCalendar = schoolCalendar;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? studentId, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        var validatedStudentId = ValidateStudentId(studentId, students);

        var model = new OrderCalendarPageViewModel
        {
            Children = students
                .Select(s => new HistoryChildOption
                {
                    StudentId = Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture),
                    Name = s.Name?.Trim() ?? string.Empty
                })
                .ToList(),
            SelectedStudentId = validatedStudentId
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Events(
        DateTime? start,
        DateTime? end,
        int? studentId,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (start is null || end is null)
        {
            return BadRequest(new { message = "Start and end dates are required." });
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        var validatedStudentId = ValidateStudentId(studentId, students);

        var fromDate = start.Value.Date;
        var toDate = end.Value.Date;
        if (fromDate >= toDate)
        {
            toDate = fromDate.AddDays(1);
        }

        var items = await _mealOrderRepository.GetOrderCalendarItemsAsync(
            guardianId,
            validatedStudentId,
            fromDate,
            toDate,
            cancellationToken);

        var events = BuildCalendarEvents(items);
        return Json(events);
    }

    [HttpGet]
    public async Task<IActionResult> SchoolDays(
        DateTime? start,
        DateTime? end,
        int? studentId,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (start is null || end is null)
        {
            return BadRequest(new { message = "Start and end dates are required." });
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        var validatedStudentId = ValidateStudentId(studentId, students);

        var fromDate = start.Value.Date;
        var toDate = end.Value.Date;
        if (fromDate >= toDate)
        {
            toDate = fromDate.AddDays(1);
        }

        var schoolIds = new List<int>();
        if (validatedStudentId is int selectedStudentId)
        {
            var schoolId = await _studentRepository.GetStudentSchoolIdAsync(selectedStudentId, cancellationToken);
            if (schoolId is > 0)
            {
                schoolIds.Add(schoolId.Value);
            }
        }
        else
        {
            foreach (var student in students)
            {
                var sid = Convert.ToInt32(student.UserId, CultureInfo.InvariantCulture);
                var schoolId = await _studentRepository.GetStudentSchoolIdAsync(sid, cancellationToken);
                if (schoolId is > 0)
                {
                    schoolIds.Add(schoolId.Value);
                }
            }
        }

        var days = await _schoolCalendar.GetMergedRangeAsync(schoolIds, fromDate, toDate, cancellationToken);
        var payload = days
            .Where(d => d.Status is SchoolDayStatus.Holiday or SchoolDayStatus.HalfDay)
            .Select(d => new OrderCalendarSchoolDayDto
            {
                Date = d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Status = (byte)d.Status,
                StatusLabel = d.Status switch
                {
                    SchoolDayStatus.Holiday => "Holiday",
                    SchoolDayStatus.HalfDay => "Half day",
                    _ => "Full day"
                },
                Title = d.Title,
                IsException = d.IsException
            })
            .ToList();

        return Json(payload);
    }

    private static int? ValidateStudentId(int? studentId, IReadOnlyList<StudentBasicListItemDto> students)
    {
        if (studentId is null or <= 0)
        {
            return null;
        }

        return students.Any(s => Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture) == studentId.Value)
            ? studentId
            : null;
    }

    private static IReadOnlyList<OrderCalendarEventDto> BuildCalendarEvents(IReadOnlyList<OrderCalendarItemDto> items)
    {
        return items
            .GroupBy(x => new { MealDate = x.MealDate.Date, x.StudentId, x.StudentName, x.OrderTypeId })
            .Select(group =>
            {
                var orderTypeId = group.Key.OrderTypeId;
                var itemNames = group
                    .Select(x => FormatItemLabel(x.ItemName, x.Quantity))
                    .Where(x => !string.IsNullOrWhiteSpace(x))
                    .ToList();

                var previewNames = itemNames.Take(2).ToList();
                var remaining = itemNames.Count - previewNames.Count;
                var preview = string.Join(", ", previewNames);
                if (remaining > 0)
                {
                    preview = $"{preview} +{remaining} more";
                }

                var studentName = string.IsNullOrWhiteSpace(group.Key.StudentName)
                    ? "Child"
                    : group.Key.StudentName;

                var title = string.IsNullOrWhiteSpace(preview)
                    ? studentName
                    : $"{studentName} — {preview}";

                var colors = GetOrderTypeColors(orderTypeId);
                var eventItems = group
                    .GroupBy(x => new { x.OrderId, x.ItemName, x.ItemPrice, x.OrderTypeId })
                    .Select(g => new OrderCalendarEventItemDto
                    {
                        OrderId = g.Key.OrderId,
                        ItemName = g.Key.ItemName,
                        ItemPrice = g.Key.ItemPrice,
                        Quantity = g.Sum(x => x.Quantity),
                        OrderTypeId = g.Key.OrderTypeId
                    })
                    .OrderBy(x => x.ItemName)
                    .ToList();

                return new OrderCalendarEventDto
                {
                    Id = $"{group.Key.MealDate:yyyyMMdd}-{group.Key.StudentId}-{orderTypeId}",
                    Title = title,
                    Start = group.Key.MealDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Color = colors.Background,
                    BorderColor = colors.Border,
                    TextColor = colors.Text,
                    ExtendedProps = new OrderCalendarEventExtendedProps
                    {
                        MealDate = group.Key.MealDate.ToString("dddd, MMMM d, yyyy", CultureInfo.InvariantCulture),
                        StudentId = group.Key.StudentId,
                        StudentName = studentName,
                        OrderTypeId = orderTypeId,
                        OrderTypeLabel = GetOrderTypeLabel(orderTypeId),
                        Items = eventItems
                    }
                };
            })
            .OrderBy(x => x.Start)
            .ThenBy(x => x.ExtendedProps.StudentName)
            .ToList();
    }

    private static string FormatItemLabel(string itemName, int quantity)
    {
        var name = itemName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return quantity > 1 ? $"{name} x{quantity}" : name;
    }

    private static string GetOrderTypeLabel(int orderTypeId) =>
        orderTypeId switch
        {
            (int)TransactionTypeEnum.MealOrder => "Meal Plan",
            (int)TransactionTypeEnum.A_La_Carte => "A La Carte",
            _ => "Order"
        };

    private static (string Background, string Border, string Text) GetOrderTypeColors(int orderTypeId) =>
        orderTypeId switch
        {
            (int)TransactionTypeEnum.MealOrder => ("#ede9fe", "#7c3aed", "#5b21b6"),
            (int)TransactionTypeEnum.A_La_Carte => ("#e0f2fe", "#0284c7", "#075985"),
            _ => ("#f3f4f6", "#9ca3af", "#374151")
        };
}
