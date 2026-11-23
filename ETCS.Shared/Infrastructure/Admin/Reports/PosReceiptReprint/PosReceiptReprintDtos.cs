using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.PosReceiptReprint;

public sealed class PosReceiptReprintSearchFilter
{
    public DateTime From { get; set; }

    public DateTime To { get; set; }

    public string? TransactionId { get; set; }

    public string? CustomerId { get; set; }

    public string? TerminalCode { get; set; }

    public string? SchoolCode { get; set; }

    public IReadOnlyList<string> AllowedSchoolCodes { get; set; } = [];

    public int Start { get; set; }

    public int Length { get; set; } = DataTableRequest.DefaultPageSize;

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class PosReceiptReprintListRequest
{
    public int Draw { get; set; }

    public int Start { get; set; }

    public int Length { get; set; } = DataTableRequest.DefaultPageSize;

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public string? TransactionId { get; set; }

    public string? CustomerId { get; set; }

    public string? TerminalCode { get; set; }

    public int SchoolId { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class PosReceiptReprintPagedResult
{
    public int Draw { get; init; }

    public int RecordsTotal { get; init; }

    public int RecordsFiltered { get; init; }

    public IReadOnlyList<PosReceiptReprintSearchRowDto> Data { get; init; } = [];

    public bool Success { get; init; } = true;

    public string? Message { get; init; }
}

public sealed class PosReceiptReprintSearchRowDto
{
    public long AccessLogId { get; init; }

    public DateTime SaleDateTime { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string CustomerId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public int TransactionType { get; init; }

    public string PaymentMode { get; init; } = string.Empty;

    public string TypeLabel { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string TerminalCode { get; init; } = string.Empty;

    public string TerminalName { get; init; } = string.Empty;

    public string BranchCode { get; init; } = string.Empty;

    public string SchoolName { get; init; } = string.Empty;

    public bool HasLineItems { get; init; }
}

public sealed class PosReceiptReprintLineDto
{
    public string SkuCode { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public int Quantity { get; init; } = 1;
}

public sealed class PosReceiptReprintDetailDto
{
    public long AccessLogId { get; init; }

    public DateTime SaleDateTime { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string CustomerId { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public int TransactionType { get; init; }

    public string PaymentMode { get; init; } = string.Empty;

    public string TypeLabel { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string TerminalCode { get; init; } = string.Empty;

    public string TerminalName { get; init; } = string.Empty;

    public string BranchCode { get; init; } = string.Empty;

    public string SchoolName { get; init; } = string.Empty;

    public bool IsUndo { get; init; }

    public bool HasLineItems { get; init; }

    public IReadOnlyList<PosReceiptReprintLineDto> Lines { get; init; } = [];

    public string LinesMissingNotice { get; init; } = string.Empty;
}
