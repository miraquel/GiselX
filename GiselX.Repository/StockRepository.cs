using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public class StockRepository : IStockRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public StockRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task UploadAsync(IEnumerable<Stock> stocks, CancellationToken cancellationToken)
    {
        var sqlServerConnection = _dbConnection as SqlConnection ?? throw new InvalidOperationException("Invalid SQL connection");

        using var sqlBulkCopy = new SqlBulkCopy(sqlServerConnection, SqlBulkCopyOptions.Default, _dbTransaction as SqlTransaction);

        sqlBulkCopy.DestinationTableName = "Stock";
        sqlBulkCopy.BatchSize = 1000;

        var enumerable = stocks as Stock[] ?? stocks.ToArray();
        if (enumerable.Length == 0)
        {
            throw new ArgumentException("No stocks to upload.", nameof(stocks));
        }

        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(Stock.Id), typeof(int));
        dataTable.Columns.Add(nameof(Stock.ProductId), typeof(string));
        dataTable.Columns.Add(nameof(Stock.ProductName), typeof(string));
        dataTable.Columns.Add(nameof(Stock.ProductPackaging), typeof(string));
        dataTable.Columns.Add(nameof(Stock.ProductPcsInCtn), typeof(int));
        dataTable.Columns.Add(nameof(Stock.ProductNetto), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.ProductUnit), typeof(string));
        dataTable.Columns.Add(nameof(Stock.SaldoAwal), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.SaldoMasukPO), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.SaldoAkhir), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.BatchNumber), typeof(string));
        dataTable.Columns.Add(nameof(Stock.ExpiredDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(Stock.CompanyId), typeof(int));
        dataTable.Columns.Add(nameof(Stock.CreatedDate), typeof(DateTime));

        foreach (var stock in enumerable)
        {
            dataTable.Rows.Add(
                DBNull.Value,
                stock.ProductId,
                stock.ProductName,
                stock.ProductPackaging,
                stock.ProductPcsInCtn,
                stock.ProductNetto,
                stock.ProductUnit,
                stock.SaldoAwal,
                stock.SaldoMasukPO,
                stock.SaldoAkhir,
                stock.BatchNumber,
                stock.ExpiredDate,
                stock.CompanyId,
                stock.CreatedDate
            );
        }

        await sqlBulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    public async Task<IEnumerable<Stock>> SelectByCustAsync(int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselStock_SelectByCust";

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

        return await _dbConnection.QueryAsync<Stock>(command);
    }

    public async Task<IEnumerable<Stock>> SelectByCustPeriodAsync(int companyId, int year, int month, CancellationToken cancellationToken)
    {
        const string query = "GiselStock_SelectByCustPeriod";

        var parameters = new DynamicParameters();
        parameters.Add("@CompanyId", companyId);
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

        return await _dbConnection.QueryAsync<Stock>(command);
    }

    public async Task<PagedList<Stock>> SelectAsync(PagedListRequest pagedListRequest, int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselStock_Select";

        var parameters = new DynamicParameters();
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => typeof(Stock).GetProperty(parameter.Key)?.CanRead is true))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                continue;
            }

            var propertyInfo = typeof(Stock).GetProperty(parameter.Key);
            if (propertyInfo == null)
            {
                throw new Exception($"Property {parameter.Key} is not found.");
            }

            switch (propertyInfo.PropertyType)
            {
                case { } t when t == typeof(string):
                    parameters.Add($"@{parameter.Key}", parameter.Value.Contains('*') || parameter.Value.Contains('%')
                        ? parameter.Value.Replace('*', '%')
                        : parameter.Value);
                    break;
                case { } t when t == typeof(int):
                    if (int.TryParse(parameter.Value, out var intValue))
                        parameters.Add($"@{parameter.Key}", intValue);
                    break;
                case { } t when t == typeof(decimal):
                    if (decimal.TryParse(parameter.Value, out var decimalValue))
                        parameters.Add($"@{parameter.Key}", decimalValue);
                    break;
                case { } t when t == typeof(DateTime):
                    if (DateTime.TryParse(parameter.Value, out var dateTimeValue) ||
                        DateTime.TryParseExact(parameter.Value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTimeValue))
                        parameters.Add($"@{parameter.Key}", dateTimeValue);
                    break;
                default:
                    throw new NotSupportedException($"Type {propertyInfo.PropertyType} is not supported.");
            }
        }

        parameters.Add("@PageNumber", pagedListRequest.PageNumber);
        parameters.Add("@PageSize", pagedListRequest.PageSize);
        parameters.Add("@CompanyId", companyId);
        parameters.Add("@SortOrder", pagedListRequest.IsSortAscending ? "ASC" : "DESC");
        parameters.Add("@SortColumn", pagedListRequest.SortBy);

        if (pagedListRequest.Filters.TryGetValue("Year", out var yearFilter) && int.TryParse(yearFilter, out var year))
            parameters.Add("@Year", year);

        if (pagedListRequest.Filters.TryGetValue("Month", out var monthFilter) && int.TryParse(monthFilter, out var month))
            parameters.Add("@Month", month);

        await _dbConnection.ExecuteAsync("SET ARITHABORT ON", transaction: _dbTransaction);
        var command = new CommandDefinition(
            query,
            parameters,
            _dbTransaction,
            cancellationToken: cancellationToken,
            commandType: CommandType.StoredProcedure);

        var result = await _dbConnection.QueryMultipleAsync(command);
        var items = result.Read<Stock>().ToList();
        var totalCount = result.ReadSingle<int>();

        return new PagedList<Stock>(items, pagedListRequest.PageNumber, pagedListRequest.PageSize, totalCount);
    }

    public async Task<IEnumerable<Period>> SelectPeriodsAsync(int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselStock_SelectPeriods";

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

        return await _dbConnection.QueryAsync<Period>(command);
    }
}
