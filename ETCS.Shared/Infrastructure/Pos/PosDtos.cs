namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosSchoolDto
{
    public int SchoolId { get; init; }
    public string SchoolName { get; init; } = string.Empty;
    public string SchoolCode { get; init; } = string.Empty;
}

public sealed class PosTerminalDto
{
    public string TerminalCode { get; init; } = string.Empty;
    public string TerminalName { get; init; } = string.Empty;
    public string BranchCode { get; init; } = string.Empty;
    public string TerminalCodeNumeric { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public int? SchoolId { get; init; }
    public bool IsActive { get; init; }
}

public sealed class PosCategoryDto
{
    public int CategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}

public sealed class PosCatalogItemDto
{
    public int Id { get; init; }
    public string ItemCode { get; init; } = string.Empty;
    public string ItemName { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string ImageName { get; init; } = string.Empty;
    public int MealCategoryId { get; init; }
    public string CategoryName { get; init; } = string.Empty;
}

public sealed class PosSpendInfoDto
{
    public string CustomerId { get; init; } = string.Empty;
    public int? StudentId { get; init; }
    public decimal DailySpent { get; init; }
    public decimal WeeklySpent { get; init; }
    public decimal DailySpendLimit { get; init; }
    public decimal WeeklySpendLimit { get; init; }
    public decimal DailyRemaining { get; init; }
    public decimal WeeklyRemaining { get; init; }
    public bool IsDailyLimitExceeded { get; init; }
    public bool IsWeeklyLimitExceeded { get; init; }
}

public sealed class PosCashPurchaseRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string CreditCardNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string BranchCode { get; init; } = "1";
    public string TerminalCode { get; init; } = string.Empty;
    public int TerminalCodeNumeric { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public string Description { get; init; } = "Cash Purchase";
}

public sealed class PosPostPurchaseLineRequest
{
    public string SkuCode { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class PosPostPurchaseRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public string IpAddress { get; init; } = string.Empty;
    public string BranchCode { get; init; } = "1";
    public IReadOnlyList<PosPostPurchaseLineRequest> Lines { get; init; } = [];
}

public sealed class PosSpendLimitRollbackRequest
{
    public string CustomerId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class PosLegacyOperationResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class PosManualTopupRequest
{
    public string CardNumber { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string? TransactionId { get; init; }
    public string? Remarks { get; init; }
}

public sealed class PosManualTopupResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public string TransactionId { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public decimal Balance { get; init; }
}

public sealed class PosCardCheckResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

public sealed class PosAccessLogResponse
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public long AccessLogId { get; init; }
}
