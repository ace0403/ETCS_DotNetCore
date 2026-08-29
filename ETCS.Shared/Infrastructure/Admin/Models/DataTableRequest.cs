namespace ETCS.Shared.Infrastructure.Admin.Models;

public sealed class DataTableRequest
{
    public const int DefaultPageSize = 25;

    public int Draw { get; set; }
    public int Start { get; set; }
    public int Length { get; set; } = DefaultPageSize;
    public int? SchoolId { get; set; }
    public int? OrderTypeId { get; set; }
    public DataTableSearchParam? Search { get; set; }
    public List<DataTableOrderParam>? Order { get; set; }
    public List<DataTableColumnParam>? Columns { get; set; }

    public string SearchText => (Search?.Value ?? string.Empty).Trim();

    public int PageSize => Length <= 0 ? DefaultPageSize : Length;
}

public sealed class DataTableSearchParam
{
    public string? Value { get; set; }
    public bool Regex { get; set; }
}

public sealed class DataTableOrderParam
{
    public int Column { get; set; }
    public string? Dir { get; set; }
}

public sealed class DataTableColumnParam
{
    public string? Data { get; set; }
    public string? Name { get; set; }
    public bool Searchable { get; set; }
    public bool Orderable { get; set; }
}
