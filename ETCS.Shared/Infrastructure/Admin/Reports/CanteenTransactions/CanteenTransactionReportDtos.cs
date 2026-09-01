using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;

public sealed class CanteenTransactionReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolCodesCsv { get; set; }
    public string? Branch { get; set; }
    public string? TransactionType { get; set; }
    public string? StudentCardNo { get; set; }
}

public sealed class CanteenTransactionReportListRequest
{
    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DataTableRequest.DefaultPageSize;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? SchoolCode { get; set; }
    public string? SchoolCodesCsv { get; set; }
    public string? Branch { get; set; }
    public string? TransactionType { get; set; }
    public string? StudentCardNo { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class CanteenTransactionReportRowDto
{
    public DateTime? DateTime { get; init; }
    public string StudCode { get; init; } = string.Empty;
    public string StudFirstName { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public decimal? Price { get; init; }
    public int? Quantity { get; init; }
    public decimal Amount { get; init; }
    public decimal? BalPrepaid { get; init; }
    public string Location { get; init; } = string.Empty;
}

internal sealed class CanteenTransactionSpRow
{
    public DateTime? Datetime { get; init; }
    public string? StudCode { get; init; }
    public string? StudFirstName { get; init; }
    public string? TransactionType { get; init; }
    public string? Price { get; init; }
    public int? Quantity { get; init; }
    public decimal Amount { get; init; }
    public decimal? BalPrepaid { get; init; }
    public string? Location { get; init; }

    public CanteenTransactionReportRowDto ToDto() => new()
    {
        DateTime = Datetime,
        StudCode = StudCode?.Trim() ?? string.Empty,
        StudFirstName = StudFirstName?.Trim() ?? string.Empty,
        TransactionType = TransactionType?.Trim() ?? string.Empty,
        Price = ParseDecimal(Price),
        Quantity = Quantity,
        Amount = Amount,
        BalPrepaid = BalPrepaid,
        Location = Location?.Trim() ?? string.Empty
    };

    private static decimal? ParseDecimal(string? value) =>
        decimal.TryParse(value, out var parsed) ? parsed : null;
}

public sealed class SchoolCodeLookupDto
{
    public string Code { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
}

public sealed class TerminalLookupDto
{
    public string TerminalCode { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}

public sealed class CanteenTransactionReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<CanteenTransactionReportRowDto> Data { get; init; } = [];
}
