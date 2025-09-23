using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public class ServiceLevelRepository : IServiceLevelRepository
{
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
        
        dataTable.Columns.Add("SoId", typeof(string));
        dataTable.Columns.Add("SoCreateDate", typeof(DateTime));
        dataTable.Columns.Add("LeadTimeDlv", typeof(int));
        dataTable.Columns.Add("LeadTimeRct", typeof(int));
        dataTable.Columns.Add("ItemId", typeof(string));
        dataTable.Columns.Add("ItemName", typeof(string));
        dataTable.Columns.Add("SoQty", typeof(decimal));
        dataTable.Columns.Add("Unit", typeof(string));
        dataTable.Columns.Add("KgPerUnit", typeof(decimal));
        dataTable.Columns.Add("DlvDateRequest", typeof(DateTime));
        dataTable.Columns.Add("RctDateRequest", typeof(DateTime));
        dataTable.Columns.Add("DoQty", typeof(decimal));
        dataTable.Columns.Add("DoDate", typeof(DateTime));
        dataTable.Columns.Add("ReceiptDate", typeof(DateTime));
        dataTable.Columns.Add("CreatedBy", typeof(string));
        dataTable.Columns.Add("CreatedDate", typeof(DateTime));

        foreach (var transDist in enumerable)
        {
            dataTable.Rows.Add(
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
                transDist.CreatedBy,
                transDist.CreatedDate
            );
        }
        
        await sqlBulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    public async Task<IEnumerable<ServiceLevel>> SelectByCustAsync(string custCode, CancellationToken cancellationToken)
    {
        const string query = "Gisel_SelectByCust";
        
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

        return await _dbConnection.QueryAsync<ServiceLevel>(command);
    }

    public async Task DeleteAsync(ServiceLevel serviceLevel, CancellationToken cancellationToken)
    {
        const string query = "Gisel_del";
        var parameters = new DynamicParameters();
        parameters.Add("@SoId", serviceLevel.SoId);
        parameters.Add("@ItemId", serviceLevel.ItemId);
        parameters.Add("@CreatedBy", serviceLevel.CreatedBy);
        
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
        const string query = "Gisel_Select";
        
        var parameters = new DynamicParameters();
        // loop Filters
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => typeof(ServiceLevel).GetProperty(parameter.Key)?.CanRead is true))
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
}