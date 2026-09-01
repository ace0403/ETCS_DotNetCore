using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;

public sealed class AdminTransactionReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolCodesCsv { get; set; }
    public string? TerminalCode { get; set; }
    public string? TransactionType { get; set; }
    public string? StudentCardNo { get; set; }
    public string? TransactionId { get; set; }
}

public sealed class AdminTransactionReportListRequest
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
    public string? StudentCardNo { get; set; }
    public string? TransactionId { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class AdminTransactionReportRowDto
{
    public DateTime? DateTime { get; init; }
    public string StudentId { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Class { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Vat { get; init; }
    public string Terminal { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
}

internal sealed class AdminTransactionSpRow
{
    public DateTime? Datetime { get; init; }
    public string? StudentID { get; init; }
    public decimal Amount { get; init; }
    public string? Name { get; init; }
    public string? Class { get; init; }
    public string? TransactionType { get; init; }
    public decimal VAT { get; init; }
    public string? Terminal { get; init; }
    public string? TransactionID { get; init; }

    public AdminTransactionReportRowDto ToDto() => new()
    {
        DateTime = Datetime,
        StudentId = StudentID?.Trim() ?? string.Empty,
        Name = Name?.Trim() ?? string.Empty,
        Class = Class?.Trim() ?? string.Empty,
        Amount = Amount,
        Vat = VAT,
        Terminal = Terminal?.Trim() ?? string.Empty,
        TransactionType = TransactionType?.Trim() ?? string.Empty,
        TransactionId = TransactionID?.Trim() ?? string.Empty
    };
}

public sealed class AdminTransactionReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<AdminTransactionReportRowDto> Data { get; init; } = [];
}
