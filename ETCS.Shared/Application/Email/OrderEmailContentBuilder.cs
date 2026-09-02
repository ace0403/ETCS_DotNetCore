using System.Globalization;
using System.Net;
using System.Text;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;

namespace ETCS.Shared.Application.Email;

public sealed class OrderEmailContentBuilder : IOrderEmailContentBuilder
{
    private const string SupportEmail = "schoolcanteen@etasteuae.com";

    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IStudentRepository _studentRepository;

    public OrderEmailContentBuilder(
        IMealOrderRepository mealOrderRepository,
        IMealRepository mealRepository,
        IStudentRepository studentRepository)
    {
        _mealOrderRepository = mealOrderRepository;
        _mealRepository = mealRepository;
        _studentRepository = studentRepository;
    }

    public async Task<string> BuildOrderSuccessContentAsync(
        int guardianId,
        int studentId,
        int orderTypeId,
        string orderId,
        decimal total,
        CancellationToken cancellationToken)
    {
        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, orderId, cancellationToken);
        if (order is null || order.LineItems.Count == 0)
        {
            return BuildFallbackContent(studentId, total);
        }

        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, customerId: null, cancellationToken);
        var student = students.FirstOrDefault(s => Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture) == studentId);

        var studentIdLabel = ResolveStudentId(student, studentId);
        var studentName = ResolveStudentName(student, order.StudentName, studentId);
        var studentClass = BuildClassLabel(student);

        var rows = orderTypeId == (int)TransactionTypeEnum.A_La_Carte
            ? await BuildAlaCarteRowsAsync(order, cancellationToken)
            : await BuildComboRowsAsync(order, cancellationToken);

        return BuildLegacyHtml(studentIdLabel, studentName, studentClass, rows, total);
    }

    private async Task<IReadOnlyList<OrderEmailRow>> BuildAlaCarteRowsAsync(
        OrderDetailDto order,
        CancellationToken cancellationToken)
    {
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

        return order.LineItems
            .Select(line =>
            {
                MealItemDto? menuItem = null;
                if (line.ItemId is > 0)
                {
                    menuLookup.TryGetValue((line.ItemId.Value, line.MealDate.Date), out menuItem);
                }

                var menuItemName = string.IsNullOrWhiteSpace(line.ItemName)
                    ? menuItem?.ItemName ?? "Meal item"
                    : line.ItemName.Trim();

                var mealType = menuItem?.MealTypeName?.Trim();
                if (string.IsNullOrWhiteSpace(mealType))
                {
                    mealType = menuItem?.MealSessionName?.Trim() ?? string.Empty;
                }

                return new OrderEmailRow(
                    line.MealDate,
                    mealType,
                    menuItemName,
                    line.ItemPrice);
            })
            .ToList();
    }

    private async Task<IReadOnlyList<OrderEmailRow>> BuildComboRowsAsync(
        OrderDetailDto order,
        CancellationToken cancellationToken)
    {
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

        return order.LineItems
            .Select(line =>
            {
                if (line.ItemId is > 0)
                {
                    itemLookup.TryGetValue((line.ItemId.Value, line.MealDate.Date), out var menuItem);
                    var itemName = string.IsNullOrWhiteSpace(line.ItemName)
                        ? menuItem?.ItemName ?? "Add-on"
                        : line.ItemName.Trim();
                    var mealType = menuItem?.MealTypeName?.Trim();
                    if (string.IsNullOrWhiteSpace(mealType))
                    {
                        mealType = menuItem?.MealSessionName?.Trim() ?? string.Empty;
                    }

                    return new OrderEmailRow(line.MealDate, mealType, itemName, line.ItemPrice);
                }

                MealPackageDto? package = null;
                if (line.PackageId is > 0)
                {
                    packageLookup.TryGetValue((line.PackageId.Value, line.MealDate.Date), out package);
                }

                var packageName = string.IsNullOrWhiteSpace(line.ItemName)
                    ? package?.PackageName ?? "Meal combo"
                    : line.ItemName.Trim();
                var menuLabel = !string.IsNullOrWhiteSpace(package?.ItemsName)
                    ? package.ItemsName.Trim()
                    : packageName;

                var comboMealType = package?.MealTypeName?.Trim();
                if (string.IsNullOrWhiteSpace(comboMealType))
                {
                    comboMealType = package?.MealSessionName?.Trim() ?? string.Empty;
                }

                return new OrderEmailRow(line.MealDate, comboMealType, menuLabel, line.ItemPrice);
            })
            .ToList();
    }

    private static string BuildLegacyHtml(
        string studentIdLabel,
        string studentName,
        string studentClass,
        IReadOnlyList<OrderEmailRow> rows,
        decimal total)
    {
        var amountText = total.ToString("F2", CultureInfo.InvariantCulture);
        var sb = new StringBuilder();
        const string cellStyle = "padding:8px 10px;border:1px solid #000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;font-size:13px;color:#000000;";
        const string headerStyle = cellStyle + "background-color:#f2f2f2;font-weight:bold;";

        sb.Append("<p style=\"margin:0 0 12px 0;font-size:14px;line-height:1.8;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">");
        sb.Append("<strong>StudentID:</strong> ").Append(HtmlEncode(studentIdLabel)).Append("<br />");
        sb.Append("<strong>Student Name:</strong> ").Append(HtmlEncode(studentName)).Append("<br />");
        sb.Append("<strong>Class:</strong> ").Append(HtmlEncode(studentClass));
        sb.Append("</p>");

        sb.Append("<table role=\"presentation\" width=\"100%\" cellpadding=\"0\" cellspacing=\"0\" ");
        sb.Append("style=\"border-collapse:collapse;width:100%;margin:12px 0 16px 0;font-family:Segoe UI,Arial,Helvetica,sans-serif;font-size:13px;color:#000000;\">");
        sb.Append("<thead><tr>");
        sb.Append("<th align=\"left\" style=\"").Append(headerStyle).Append("\">Date</th>");
        sb.Append("<th align=\"left\" style=\"").Append(headerStyle).Append("\">Meal Type</th>");
        sb.Append("<th align=\"left\" style=\"").Append(headerStyle).Append("\">Menu Item</th>");
        sb.Append("<th align=\"right\" style=\"").Append(headerStyle).Append("\">Total Amount</th>");
        sb.Append("</tr></thead><tbody>");

        foreach (var row in rows)
        {
            sb.Append("<tr>");
            sb.Append("<td align=\"left\" style=\"").Append(cellStyle).Append("\">")
                .Append(HtmlEncode(row.MealDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture))).Append("</td>");
            sb.Append("<td align=\"left\" style=\"").Append(cellStyle).Append("\">")
                .Append(HtmlEncode(row.MealType)).Append("</td>");
            sb.Append("<td align=\"left\" style=\"").Append(cellStyle).Append("\">")
                .Append(HtmlEncode(row.MenuItem)).Append("</td>");
            sb.Append("<td align=\"right\" style=\"").Append(cellStyle).Append("\">AED ")
                .Append(row.Amount.ToString("F2", CultureInfo.InvariantCulture)).Append("</td>");
            sb.Append("</tr>");
        }

        sb.Append("<tr>");
        sb.Append("<td colspan=\"3\" align=\"left\" style=\"").Append(cellStyle).Append("font-weight:bold;\">Total</td>");
        sb.Append("<td align=\"right\" style=\"").Append(cellStyle).Append("font-weight:bold;\">AED ")
            .Append(amountText).Append("</td>");
        sb.Append("</tr>");
        sb.Append("</tbody></table>");

        sb.Append("<p style=\"margin:0 0 6px 0;font-size:14px;line-height:1.8;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">");
        sb.Append("<strong>Online Paid Amount:</strong> AED ").Append(amountText).Append("<br />");
        sb.Append("<strong>Net Amount:</strong> AED ").Append(amountText);
        sb.Append("</p>");

        sb.Append("<p style=\"margin:20px 0 12px 0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">");
        sb.Append("Should you have any questions, or need further information, please do not hesitate to contact us at ");
        sb.Append("<a href=\"mailto:").Append(SupportEmail).Append("\" style=\"color:#0563c1;\">").Append(SupportEmail).Append("</a>.");
        sb.Append("</p>");

        sb.Append("<p style=\"margin:0 0 20px 0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">");
        sb.Append("<strong>Note: Kindly note once you have made a successful payment for our services, we will not provide any Cancellations or Refunds. All payments are considered as final.</strong>");
        sb.Append("</p>");

        sb.Append("<p style=\"margin:0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;\">");
        sb.Append("Warm Regards,<br />Emirate Taste Catering.");
        sb.Append("</p>");

        return sb.ToString();
    }

    private static string BuildFallbackContent(int studentId, decimal total)
    {
        var amountText = total.ToString("F2", CultureInfo.InvariantCulture);
        return $"""
            <p style="margin:0 0 12px 0;font-size:14px;line-height:1.8;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
            <strong>StudentID:</strong> {HtmlEncode(studentId.ToString(CultureInfo.InvariantCulture))}
            </p>
            <p style="margin:0 0 6px 0;font-size:14px;line-height:1.8;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
            <strong>Online Paid Amount:</strong> AED {amountText}<br />
            <strong>Net Amount:</strong> AED {amountText}
            </p>
            <p style="margin:20px 0 12px 0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
            Should you have any questions, or need further information, please do not hesitate to contact us at <a href="mailto:{SupportEmail}" style="color:#0563c1;">{SupportEmail}</a>.
            </p>
            <p style="margin:0 0 20px 0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
            <strong>Note: Kindly note once you have made a successful payment for our services, we will not provide any Cancellations or Refunds. All payments are considered as final.</strong>
            </p>
            <p style="margin:0;font-size:14px;line-height:1.6;color:#000000;font-family:Segoe UI,Arial,Helvetica,sans-serif;">
            Warm Regards,<br />Emirate Taste Catering.
            </p>
            """;
    }

    private static string ResolveStudentId(StudentListingDto? student, int studentId)
    {
        if (!string.IsNullOrWhiteSpace(student?.StudCode))
        {
            return student.StudCode.Trim();
        }

        if (!string.IsNullOrWhiteSpace(student?.Cardid))
        {
            return student.Cardid.Trim();
        }

        return studentId.ToString(CultureInfo.InvariantCulture);
    }

    private static string ResolveStudentName(StudentListingDto? student, string orderStudentName, int studentId)
    {
        if (!string.IsNullOrWhiteSpace(student?.Name))
        {
            return student.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(orderStudentName))
        {
            return orderStudentName.Trim();
        }

        return studentId.ToString(CultureInfo.InvariantCulture);
    }

    private static string BuildClassLabel(StudentListingDto? student)
    {
        if (student is null)
        {
            return string.Empty;
        }

        var std = NormalizeClassPart(student.Std);
        var className = NormalizeClassPart(student.ClassName);
        var groupName = NormalizeClassPart(student.GroupName);

        if (string.IsNullOrEmpty(std) && string.IsNullOrEmpty(className))
        {
            return groupName ?? string.Empty;
        }

        if (string.IsNullOrEmpty(className) || string.Equals(std, className, StringComparison.OrdinalIgnoreCase))
        {
            return std ?? string.Empty;
        }

        if (string.IsNullOrEmpty(std))
        {
            return className ?? string.Empty;
        }

        if (className.Contains(std, StringComparison.OrdinalIgnoreCase))
        {
            return std;
        }

        if (std.Contains(className, StringComparison.OrdinalIgnoreCase))
        {
            return std;
        }

        var classPrimary = className
            .Split(" / ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault() ?? className;

        if (std.Contains(classPrimary, StringComparison.OrdinalIgnoreCase))
        {
            return std;
        }

        return $"{std} / {classPrimary}";
    }

    private static string? NormalizeClassPart(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed == "-" ? null : trimmed;
    }

    private static string HtmlEncode(string value) => WebUtility.HtmlEncode(value);

    private sealed record OrderEmailRow(DateTime MealDate, string MealType, string MenuItem, decimal Amount);
}
