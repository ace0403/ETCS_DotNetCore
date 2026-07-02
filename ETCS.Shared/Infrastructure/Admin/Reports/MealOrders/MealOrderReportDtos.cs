using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;

public sealed class MealOrderReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? SchoolId { get; set; }
}

public sealed class MealOrderReportListRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DataTableRequest.DefaultPageSize;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SchoolId { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class MealOrderReportRowDto
{
    public string OrderDate { get; init; } = string.Empty;
    public string StudCode { get; init; } = string.Empty;
    public string StudStd { get; init; } = string.Empty;
    public string StudDiv { get; init; } = string.Empty;
    public string StudFullName { get; init; } = string.Empty;
    public string PaymentStatus { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public string Choice { get; init; } = string.Empty;
    public string DeliveryDate { get; init; } = string.Empty;
    public string Day { get; init; } = string.Empty;
    public string Items { get; init; } = string.Empty;
}

public sealed class MealOrderReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<MealOrderReportRowDto> Data { get; init; } = [];
}

public sealed class MealOrderSchoolLookupDto
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}
