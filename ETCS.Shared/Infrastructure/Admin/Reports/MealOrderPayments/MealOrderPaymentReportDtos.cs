using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;

public sealed class MealOrderPaymentReportFilter
{
    public DateTime StartDate { get; set; }

    public DateTime EndDate { get; set; }

    public string? SchoolId { get; set; }

    public int? MealSessionId { get; set; }

    public int? MealTypeId { get; set; }
}

public sealed class MealOrderPaymentReportListRequest
{
    public int Draw { get; set; }

    public int Start { get; set; }

    public int Length { get; set; } = DataTableRequest.DefaultPageSize;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public string? SchoolId { get; set; }

    public int? MealSessionId { get; set; }

    public int? MealTypeId { get; set; }

    public int PageSize => Length <= 0 ? DataTableRequest.DefaultPageSize : Length;
}

public sealed class MealOrderPaymentReportRowDto
{
    public string OrderDate { get; init; } = string.Empty;

    public string StudCode { get; init; } = string.Empty;

    public string StudStd { get; init; } = string.Empty;

    public string StudDiv { get; init; } = string.Empty;

    public string StudFullName { get; init; } = string.Empty;

    public string PaymentStatus { get; init; } = string.Empty;

    public string MealSession { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string DeliveryDate { get; init; } = string.Empty;

    public string Day { get; init; } = string.Empty;

    public string Items { get; init; } = string.Empty;
    public string SchoolName { get; init; } = string.Empty;
    public string Package { get; init; } = "PACKAGE UNKNOWN";
    public string TransactionType { get; init; } = string.Empty;
}

public sealed class MealOrderPaymentReportPagedResult
{
    public int Draw { get; init; }

    public int RecordsTotal { get; init; }

    public int RecordsFiltered { get; init; }

    public IReadOnlyList<MealOrderPaymentReportRowDto> Data { get; init; } = [];
}

public sealed class MealOrderPaymentSchoolLookupDto
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;
}
