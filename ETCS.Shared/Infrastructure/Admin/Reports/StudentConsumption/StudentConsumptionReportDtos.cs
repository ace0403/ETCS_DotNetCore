namespace ETCS.Shared.Infrastructure.Admin.Reports.StudentConsumption;

public enum StudentConsumptionRowKind
{
    Opening = 0,
    Transaction = 1,
    Total = 2
}

public sealed class StudentConsumptionReportFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int SchoolId { get; set; }
    public decimal StudentUserId { get; set; }
}

public sealed class StudentConsumptionReportListRequest
{
    public const int DefaultStudentPageSize = 10;
    public const int MaxStudentPageSize = 50;

    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DefaultStudentPageSize;
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int SchoolId { get; set; }
    public decimal StudentUserId { get; set; }

    public int StudentPageSize =>
        Length <= 0 ? DefaultStudentPageSize : Math.Min(Length, MaxStudentPageSize);
}

public sealed class StudentConsumptionStudentHeaderDto
{
    public decimal UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StudCode { get; init; } = string.Empty;
    public string Grade { get; init; } = string.Empty;
    public int SchoolId { get; init; }
}

public sealed class StudentConsumptionReportRowDto
{
    public decimal StudentUserId { get; init; }
    public string StudentName { get; init; } = string.Empty;
    public string StudCode { get; init; } = string.Empty;
    public string CustomerDetails { get; init; } = string.Empty;
    public string TransDate { get; init; } = string.Empty;
    public decimal? Debit { get; init; }
    public decimal? Credit { get; init; }
    public decimal Amount { get; init; }
    public StudentConsumptionRowKind RowKind { get; init; }
}

public sealed class StudentConsumptionReportResult
{
    public IReadOnlyList<StudentConsumptionReportRowDto> Rows { get; init; } = [];
}

public sealed class StudentConsumptionReportPagedData
{
    public int TotalStudents { get; init; }
    public IReadOnlyList<StudentConsumptionReportRowDto> Rows { get; init; } = [];
}

public sealed class StudentConsumptionStudentLookupDto
{
    public decimal UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string StudCode { get; init; } = string.Empty;
}

public sealed class StudentConsumptionReportPagedResult
{
    public int Draw { get; init; }
    public int RecordsTotal { get; init; }
    public int RecordsFiltered { get; init; }
    public IReadOnlyList<StudentConsumptionReportRowDto> Data { get; init; } = [];
    public bool Success { get; init; } = true;
    public string? Message { get; init; }
}
