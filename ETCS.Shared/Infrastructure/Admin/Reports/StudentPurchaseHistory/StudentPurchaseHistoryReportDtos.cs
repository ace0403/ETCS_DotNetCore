using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentPurchaseHistory;

public sealed class StudentPurchaseHistoryReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SchoolId { get; set; }
    public int StudentUserId { get; set; }
}

public sealed class StudentPurchaseHistoryReportListRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DataTableRequest.DefaultPageSize;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SchoolId { get; set; }
    public int StudentUserId { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class StudentPurchaseHistoryReportRowDto
{
    public string StudentName { get; init; } = string.Empty;
    public string StudentCode { get; init; } = string.Empty;
    public string Grade { get; init; } = string.Empty;
    public string Source { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public int Quantity { get; init; }
    public decimal Amount { get; init; }
    public string PurchaseDateTime { get; init; } = string.Empty;
    public string Reference { get; init; } = string.Empty;

    /// <summary>Used for chronological sorting before paging.</summary>
    public DateTime SortDateTime { get; init; }
}

public sealed class StudentPurchaseHistoryReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<StudentPurchaseHistoryReportRowDto> Data { get; init; } = [];
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
}
