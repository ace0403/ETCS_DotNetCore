namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public sealed class SchoolGradeOrderTypeConfigDto
{
    public int GradeId { get; set; }
    public bool IsNoService { get; set; }
    public List<int> OrderTypeIds { get; set; } = [];
}

public sealed class SchoolGradeOrderTypeAccessDto
{
    public bool IsConfigured { get; init; }
    public bool IsNoService { get; init; }
    public IReadOnlyList<int> OrderTypeIds { get; init; } = [];
}
