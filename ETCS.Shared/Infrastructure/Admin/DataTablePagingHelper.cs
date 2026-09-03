using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin;

public static class DataTablePagingHelper
{
    public static (string SqlColumn, string Direction) ResolveSort(
        DataTableRequest request,
        IReadOnlyDictionary<string, string> columnMap,
        string defaultSqlColumn,
        string defaultDirection = "ASC")
    {
        if (request.Order is null || request.Order.Count == 0
            || request.Columns is null || request.Columns.Count == 0)
        {
            return (defaultSqlColumn, defaultDirection);
        }

        var order = request.Order[0];
        if (order.Column < 0 || order.Column >= request.Columns.Count)
        {
            return (defaultSqlColumn, defaultDirection);
        }

        var dataKey = request.Columns[order.Column].Data;
        if (string.IsNullOrWhiteSpace(dataKey)
            || !columnMap.TryGetValue(dataKey, out var sqlColumn))
        {
            return (defaultSqlColumn, defaultDirection);
        }

        var direction = string.Equals(order.Dir, "desc", StringComparison.OrdinalIgnoreCase)
            ? "DESC"
            : "ASC";
        return (sqlColumn, direction);
    }

    public static async Task<DataTableResponse<T>> QueryPagedAsync<T>(
        DbConnection connection,
        string selectSql,
        string fromSql,
        string? baseFilterSql,
        string searchFilterSql,
        IReadOnlyDictionary<string, string> sortColumnMap,
        string defaultSortColumn,
        DataTableRequest request,
        object? extraParameters = null,
        CancellationToken cancellationToken = default,
        string defaultSortDirection = "ASC")
    {
        var search = request.SearchText;
        var pageSize = request.PageSize;
        var (sortColumn, sortDirection) = ResolveSort(
            request, sortColumnMap, defaultSortColumn, defaultSortDirection);

        var parameters = new DynamicParameters(extraParameters);
        parameters.Add("Search", search);
        parameters.Add("Start", request.Start);
        parameters.Add("Length", pageSize);

        selectSql = selectSql.Trim();
        fromSql = fromSql.Trim();
        searchFilterSql = searchFilterSql.Trim();
        baseFilterSql = baseFilterSql?.Trim();

        var whereClause = BuildWhereClause(baseFilterSql, searchFilterSql);

        var countTotalSql = string.IsNullOrWhiteSpace(baseFilterSql)
            ? $"SELECT COUNT(1) {fromSql};"
            : $"SELECT COUNT(1) {fromSql} WHERE {baseFilterSql};";

        var countFilteredSql = $"SELECT COUNT(1) {fromSql} {whereClause};";
        var dataSql = $"{selectSql} {fromSql} {whereClause} ORDER BY {sortColumn} {sortDirection} OFFSET @Start ROWS FETCH NEXT @Length ROWS ONLY;";

        var recordsTotal = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countTotalSql, parameters, cancellationToken: cancellationToken));

        var recordsFiltered = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countFilteredSql, parameters, cancellationToken: cancellationToken));

        var rows = await connection.QueryAsync<T>(
            new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken));

        return new DataTableResponse<T>
        {
            Draw = request.Draw,
            RecordsTotal = recordsTotal,
            RecordsFiltered = recordsFiltered,
            Data = rows.ToList()
        };
    }

    public static async Task<IReadOnlyList<T>> QueryAllAsync<T>(
        DbConnection connection,
        string selectSql,
        string fromSql,
        string? baseFilterSql,
        string searchFilterSql,
        IReadOnlyDictionary<string, string> sortColumnMap,
        string defaultSortColumn,
        DataTableRequest request,
        object? extraParameters = null,
        CancellationToken cancellationToken = default,
        string defaultSortDirection = "ASC")
    {
        var search = request.SearchText;
        var (sortColumn, sortDirection) = ResolveSort(
            request, sortColumnMap, defaultSortColumn, defaultSortDirection);

        var parameters = new DynamicParameters(extraParameters);
        parameters.Add("Search", search);

        selectSql = selectSql.Trim();
        fromSql = fromSql.Trim();
        searchFilterSql = searchFilterSql.Trim();
        baseFilterSql = baseFilterSql?.Trim();

        var whereClause = BuildWhereClause(baseFilterSql, searchFilterSql);
        var dataSql = $"{selectSql} {fromSql} {whereClause} ORDER BY {sortColumn} {sortDirection};";

        var rows = await connection.QueryAsync<T>(
            new CommandDefinition(dataSql, parameters, cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private static string BuildWhereClause(string? baseFilterSql, string searchFilterSql)
    {
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(baseFilterSql))
        {
            conditions.Add(baseFilterSql);
        }

        conditions.Add($"(@Search = '' OR {searchFilterSql})");
        return "WHERE " + string.Join(" AND ", conditions);
    }
}
