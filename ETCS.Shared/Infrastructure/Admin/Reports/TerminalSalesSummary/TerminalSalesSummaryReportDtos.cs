using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;

public sealed class TerminalSalesSummaryReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolCodesCsv { get; set; }
    public string? TerminalCode { get; set; }
    public string? TransactionType { get; set; }
}

public sealed class TerminalSalesSummaryReportListRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DataTableRequest.DefaultPageSize;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolCodesCsv { get; set; }
    public string? TerminalCode { get; set; }
    public string? TransactionType { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class TerminalSalesSummaryReportRowDto
{
    public string TerminalCode { get; init; } = string.Empty;
    public string TerminalName { get; init; } = string.Empty;
    public string Date { get; init; } = string.Empty;
    public int StudentsCount { get; init; }
    public decimal StudentCardPurchase { get; init; }
    public decimal CashPurchase { get; init; }
    public decimal CreditCardPurchase { get; init; }
    public decimal StudentCardManualTopup { get; init; }
    public decimal StudentCardUndoTopup { get; init; }
    public decimal OnlineStudentCardTopup { get; init; }
    public decimal UndoCashPurchase { get; init; }
}

public sealed class TerminalSalesSummaryReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<TerminalSalesSummaryReportRowDto> Data { get; init; } = [];
}
