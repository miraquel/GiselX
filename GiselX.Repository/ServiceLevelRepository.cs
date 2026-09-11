using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public class ServiceLevelRepository : IServiceLevelRepository
{
    private static readonly HashSet<string> AdministratorSelectableFields =
    [
        nameof(ServiceLevel.SoId), nameof(ServiceLevel.SoCreateDate), nameof(ServiceLevel.LeadTimeDlv),
        nameof(ServiceLevel.LeadTimeRct), nameof(ServiceLevel.ItemId), nameof(ServiceLevel.ItemName),
        nameof(ServiceLevel.SoQty), nameof(ServiceLevel.Unit), nameof(ServiceLevel.KgPerUnit),
        nameof(ServiceLevel.DlvDateRequest), nameof(ServiceLevel.RctDateRequest), nameof(ServiceLevel.DoQty),
        nameof(ServiceLevel.DoDate), nameof(ServiceLevel.ReceiptDate), nameof(ServiceLevel.CreatedDate),
        nameof(ServiceLevel.CompanyName)
    ];
    private static readonly HashSet<string> NormalSelectableFields = AdministratorSelectableFields
        .Where(field => field != nameof(ServiceLevel.CompanyName)).ToHashSet();

    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public ServiceLevelRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task UploadAsync(IEnumerable<ServiceLevel> serviceLevels, CancellationToken cancellationToken)
    {
        // insert new rofo
        var sqlServerConnection = _dbConnection as SqlConnection ?? throw new InvalidOperationException("Invalid SQL connection");
        
        using var sqlBulkCopy = new SqlBulkCopy(sqlServerConnection, SqlBulkCopyOptions.Default, _dbTransaction as SqlTransaction);
        
        sqlBulkCopy.DestinationTableName = "TransDist";
        sqlBulkCopy.BatchSize = 1000;
        
        // validate the transDists and check if there are any items to upload
        var enumerable = serviceLevels as ServiceLevel[] ?? serviceLevels.ToArray();
        if (serviceLevels == null || enumerable.Length == 0)
        {
            throw new ArgumentException("No transaction distributions to upload.", nameof(serviceLevels));
        }

        var dataTable = new DataTable();
        
        dataTable.Columns.Add(nameof(TransDist.Id), typeof(int));
        dataTable.Columns.Add(nameof(TransDist.SoId), typeof(string));
        dataTable.Columns.Add(nameof(TransDist.SoCreateDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(TransDist.LeadTimeDlv), typeof(int));
        dataTable.Columns.Add(nameof(TransDist.LeadTimeRct), typeof(int));
        dataTable.Columns.Add(nameof(TransDist.ItemId), typeof(string));
        dataTable.Columns.Add(nameof(TransDist.ItemName), typeof(string));
        dataTable.Columns.Add(nameof(TransDist.SoQty), typeof(decimal));
        dataTable.Columns.Add(nameof(TransDist.Unit), typeof(string));
        dataTable.Columns.Add(nameof(TransDist.KgPerUnit), typeof(decimal));
        dataTable.Columns.Add(nameof(TransDist.DlvDateRequest), typeof(DateTime));
        dataTable.Columns.Add(nameof(TransDist.RctDateRequest), typeof(DateTime));
        dataTable.Columns.Add(nameof(TransDist.DoQty), typeof(decimal));
        dataTable.Columns.Add(nameof(TransDist.DoDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(TransDist.ReceiptDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(TransDist.CompanyId), typeof(int));
        dataTable.Columns.Add(nameof(TransDist.CreatedBy), typeof(string));
        dataTable.Columns.Add(nameof(TransDist.CreatedDate), typeof(DateTime));

        foreach (var transDist in enumerable)
        {
            dataTable.Rows.Add(
                DBNull.Value,
                transDist.SoId,
                transDist.SoCreateDate,
                transDist.LeadTimeDlv,
                transDist.LeadTimeRct,
                transDist.ItemId,
                transDist.ItemName,
                transDist.SoQty,
                transDist.Unit,
                transDist.KgPerUnit,
                transDist.DlvDateRequest,
                transDist.RctDateRequest,
                transDist.DoQty,
                transDist.DoDate,
                transDist.ReceiptDate,
                transDist.CompanyId,
                transDist.CreatedBy,
                transDist.CreatedDate
            );
        }
        
        await sqlBulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    public async Task<IEnumerable<ServiceLevel>> SelectByCustAsync(int companyId, CancellationToken cancellationToken)
    {
        const string query = "Gisel_SelectByCust";
        
        var parameters = new DynamicParameters();
        parameters.Add("@CompanyId", companyId);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );

        return await _dbConnection.QueryAsync<ServiceLevel>(command);
    }

    public async Task<IEnumerable<ServiceLevel>> SelectByCustPeriodAsync(string custCode, int year, int month, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(custCode))
            return await _dbConnection.QueryAsync<ServiceLevel>(new CommandDefinition("""
                SELECT td.*, c.Name AS CompanyName
                FROM TransDist td INNER JOIN Company c ON c.Id=td.CompanyId
                WHERE YEAR(td.SoCreateDate)=@Year AND MONTH(td.SoCreateDate)=@Month
                ORDER BY c.Name, td.SoCreateDate, td.SoId, td.Id;
                """, new { Year = year, Month = month }, _dbTransaction,
                cancellationToken: cancellationToken));

        const string query = "Gisel_SelectByCustPeriod";
        
        var parameters = new DynamicParameters();
        parameters.Add("@CreatedBy", custCode);
        parameters.Add("@Year", year);
        parameters.Add("@Month", month);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );

        return await _dbConnection.QueryAsync<ServiceLevel>(command);
    }

    public async Task DeleteAsync(ServiceLevel serviceLevel, CancellationToken cancellationToken)
    {
        const string query = "Gisel_del";
        var parameters = new DynamicParameters();
        parameters.Add("@Id", serviceLevel.Id);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );
        
        await _dbConnection.ExecuteAsync(command);
    }

    public async Task<PagedList<ServiceLevel>> SelectAsync(PagedListRequest pagedListRequest, string custCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(custCode))
            return await SelectAllAsync(pagedListRequest, cancellationToken);

        const string query = "Gisel_Select";
        
        var parameters = new DynamicParameters();
        // loop Filters
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => NormalSelectableFields.Contains(parameter.Key)))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                continue;
            }
            
            // get the data type of the property
            var propertyInfo = typeof(ServiceLevel).GetProperty(parameter.Key);
            if (propertyInfo == null)
            {
                throw new Exception($"Property {parameter.Key} is not found.");
            }

            switch (propertyInfo.PropertyType)
            {
                case { } t when t == typeof(string):
                    if (parameter.Value.Contains('*') || parameter.Value.Contains('%'))
                    {
                        parameters.Add($"@{parameter.Key}", parameter.Value.Replace('*', '%'));
                    }
                    else
                    {
                        parameters.Add($"@{parameter.Key}", parameter.Value);
                    }
                    break;
                case { } t when t == typeof(int):
                    if (int.TryParse(parameter.Value, out var intValue))
                    {
                        parameters.Add($"@{parameter.Key}", intValue);
                    }
                    break;
                case { } t when t == typeof(decimal):
                    if (decimal.TryParse(parameter.Value, out var decimalValue))
                    {
                        parameters.Add($"@{parameter.Key}", decimalValue);
                    }
                    break;
                case { } t when t == typeof(DateTime):
                    if (DateTime.TryParse(parameter.Value, out var dateTimeValue) || DateTime.TryParseExact(parameter.Value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTimeValue))
                    {
                        parameters.Add($"@{parameter.Key}", dateTimeValue);
                    }
                    break;
                default:
                    throw new NotSupportedException($"Type {propertyInfo.PropertyType} is not supported.");
            }
        }
        
        parameters.Add("@PageNumber", pagedListRequest.PageNumber);
        parameters.Add("@PageSize", pagedListRequest.PageSize);
        parameters.Add("@CreatedBy", custCode);
        parameters.Add("@SortOrder", pagedListRequest.IsSortAscending ? "ASC" : "DESC");
        parameters.Add("@SortColumn", pagedListRequest.SortBy);
        
        if (pagedListRequest.Filters.TryGetValue("Year", out var yearFilter) && int.TryParse(yearFilter, out var year))
        {
            parameters.Add("@Year", year);
        }
        
        if (pagedListRequest.Filters.TryGetValue("Month", out var monthFilter) && int.TryParse(monthFilter, out var month))
        {
            parameters.Add("@Month", month);
        }
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure);

        var result = await _dbConnection.QueryMultipleAsync(command);
        var items = result.Read<ServiceLevel>().ToList();
        var totalCount = result.ReadSingle<int>();
        
        return new PagedList<ServiceLevel>(items, pagedListRequest.PageNumber, pagedListRequest.PageSize, totalCount);
    }

    public async Task<IEnumerable<Period>> SelectPeriodsAsync(string custCode, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(custCode))
            return await _dbConnection.QueryAsync<Period>(new CommandDefinition("""
                SELECT DISTINCT YEAR(SoCreateDate) AS [Year], MONTH(SoCreateDate) AS [Month]
                FROM TransDist
                ORDER BY [Year] DESC, [Month] DESC;
                """, transaction: _dbTransaction, cancellationToken: cancellationToken));

        const string query = "Gisel_SelectPeriods";
        
        var parameters = new DynamicParameters();
        parameters.Add("@CreatedBy", custCode);
        
        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure
        );
        
        return await _dbConnection.QueryAsync<Period>(command);
    }

    private async Task<PagedList<ServiceLevel>> SelectAllAsync(PagedListRequest request,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("PageNumber", Math.Max(1, request.PageNumber));
        parameters.Add("PageSize", Math.Clamp(request.PageSize, 1, 500));
        var clauses = new List<string>();

        if (request.Filters.TryGetValue("Year", out var yearText) && int.TryParse(yearText, out var year))
        { parameters.Add("Year", year); clauses.Add("YEAR(td.SoCreateDate)=@Year"); }
        if (request.Filters.TryGetValue("Month", out var monthText) && int.TryParse(monthText, out var month))
        { parameters.Add("Month", month); clauses.Add("MONTH(td.SoCreateDate)=@Month"); }

        foreach (var filter in request.Filters.Where(filter => AdministratorSelectableFields.Contains(filter.Key)))
        {
            if (string.IsNullOrWhiteSpace(filter.Value)) continue;
            var property = typeof(ServiceLevel).GetProperty(filter.Key);
            if (property is null) continue;
            var expression = filter.Key == nameof(ServiceLevel.CompanyName) ? "c.Name" : $"td.[{filter.Key}]";
            switch (property.PropertyType)
            {
                case { } type when type == typeof(string):
                    var text = filter.Value.Replace('*', '%');
                    parameters.Add(filter.Key, text.Contains('%') ? text : $"%{text}%");
                    clauses.Add($"{expression} LIKE @{filter.Key}");
                    break;
                case { } type when type == typeof(int):
                    if (int.TryParse(filter.Value, out var intValue))
                    { parameters.Add(filter.Key, intValue); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(decimal):
                    if (decimal.TryParse(filter.Value, out var decimalValue))
                    { parameters.Add(filter.Key, decimalValue); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(DateTime):
                    if (DateTime.TryParse(filter.Value, out var dateValue))
                    { parameters.Add(filter.Key, dateValue); clauses.Add($"CONVERT(date,{expression})=CONVERT(date,@{filter.Key})"); }
                    break;
            }
        }

        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Replace('*', '%');
            parameters.Add("Search", search.Contains('%') ? search : $"%{search}%");
            clauses.Add("(td.SoId LIKE @Search OR td.ItemId LIKE @Search OR td.ItemName LIKE @Search OR c.Name LIKE @Search)");
        }

        var filters = clauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", clauses);
        var sort = typeof(ServiceLevel).GetProperties()
            .FirstOrDefault(property => AdministratorSelectableFields.Contains(property.Name) &&
                                        string.Equals(property.Name, request.SortBy,
                                            StringComparison.OrdinalIgnoreCase))?.Name
            ?? nameof(ServiceLevel.SoCreateDate);
        var sortExpression = sort == nameof(ServiceLevel.CompanyName) ? "c.Name" : $"td.[{sort}]";
        var direction = request.IsSortAscending ? "ASC" : "DESC";
        var query = $"""
            SELECT td.*, c.Name AS CompanyName
            FROM TransDist td INNER JOIN Company c ON c.Id=td.CompanyId
            {filters}
            ORDER BY {sortExpression} {direction}, td.Id
            OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*)
            FROM TransDist td INNER JOIN Company c ON c.Id=td.CompanyId
            {filters};
            """;
        using var result = await _dbConnection.QueryMultipleAsync(new CommandDefinition(query, parameters,
            _dbTransaction, cancellationToken: cancellationToken));
        var items = (await result.ReadAsync<ServiceLevel>()).ToList();
        var totalCount = await result.ReadSingleAsync<int>();
        return new PagedList<ServiceLevel>(items, Math.Max(1, request.PageNumber),
            Math.Clamp(request.PageSize, 1, 500), totalCount);
    }
}
