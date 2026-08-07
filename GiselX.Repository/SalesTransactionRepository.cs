using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public class SalesTransactionRepository : ISalesTransactionRepository
{
    private readonly IDbConnection _dbConnection;
    private readonly IDbTransaction _dbTransaction;

    public SalesTransactionRepository(IDbConnection dbConnection, IDbTransaction dbTransaction)
    {
        _dbConnection = dbConnection;
        _dbTransaction = dbTransaction;
    }

    public async Task UploadAsync(IEnumerable<SalesTransaction> salesTransactions, CancellationToken cancellationToken)
    {
        var sqlServerConnection = _dbConnection as SqlConnection ?? throw new InvalidOperationException("Invalid SQL connection");

        using var sqlBulkCopy = new SqlBulkCopy(sqlServerConnection, SqlBulkCopyOptions.Default, _dbTransaction as SqlTransaction);

        sqlBulkCopy.DestinationTableName = "SalesTransaction";
        sqlBulkCopy.BatchSize = 1000;

        var enumerable = salesTransactions as SalesTransaction[] ?? salesTransactions.ToArray();
        if (enumerable.Length == 0)
        {
            throw new ArgumentException("No sales transactions to upload.", nameof(salesTransactions));
        }

        var dataTable = new DataTable();
        dataTable.Columns.Add(nameof(SalesTransaction.Id), typeof(int));
        dataTable.Columns.Add(nameof(SalesTransaction.CustomerId), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.CustomerName), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.CustomerAlias), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.CustomerAddress), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ProductId), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ProductBrand), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ProductName), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ProductPackaging), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ProductPcsInCtn), typeof(int));
        dataTable.Columns.Add(nameof(SalesTransaction.BatchNumber), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ExpiredDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(SalesTransaction.InvoiceNo), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.InvoiceDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(SalesTransaction.InvoiceQty), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.InvoiceUnit), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.UnitPrice), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.GrossValue), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.DiscountValue), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.DiscountPct), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.NetValue), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.InKg), typeof(decimal));
        dataTable.Columns.Add(nameof(SalesTransaction.GeisaPOId), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.ShipDate), typeof(DateTime));
        dataTable.Columns.Add(nameof(SalesTransaction.SalesmanNameGMK), typeof(string));
        dataTable.Columns.Add(nameof(SalesTransaction.CompanyId), typeof(int));
        dataTable.Columns.Add(nameof(SalesTransaction.CreatedDate), typeof(DateTime));

        foreach (var tx in enumerable)
        {
            dataTable.Rows.Add(
                DBNull.Value,
                tx.CustomerId,
                tx.CustomerName,
                tx.CustomerAlias,
                tx.CustomerAddress,
                tx.ProductId,
                tx.ProductBrand,
                tx.ProductName,
                tx.ProductPackaging,
                tx.ProductPcsInCtn,
                tx.BatchNumber,
                tx.ExpiredDate,
                tx.InvoiceNo,
                tx.InvoiceDate,
                tx.InvoiceQty,
                tx.InvoiceUnit,
                tx.UnitPrice,
                tx.GrossValue,
                tx.DiscountValue,
                tx.DiscountPct,
                tx.NetValue,
                tx.InKg,
                tx.GeisaPOId,
                tx.ShipDate,
                tx.SalesmanNameGMK,
                tx.CompanyId,
                tx.CreatedDate
            );
        }

        await sqlBulkCopy.WriteToServerAsync(dataTable, cancellationToken);
    }

    public async Task<IEnumerable<SalesTransaction>> SelectByCustAsync(int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselSales_SelectByCust";

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

        return await _dbConnection.QueryAsync<SalesTransaction>(command);
    }

    public async Task<IEnumerable<SalesTransaction>> SelectByCustPeriodAsync(int companyId, int year, int month, CancellationToken cancellationToken)
    {
        const string query = "GiselSales_SelectByCustPeriod";

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

        return await _dbConnection.QueryAsync<SalesTransaction>(command);
    }

    public async Task DeleteAsync(SalesTransaction salesTransaction, CancellationToken cancellationToken)
    {
        const string query = "GiselSales_del";
        var parameters = new DynamicParameters();
        parameters.Add("@Id", salesTransaction.Id);

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

    public async Task<PagedList<SalesTransaction>> SelectAsync(PagedListRequest pagedListRequest, int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselSales_Select";

        var parameters = new DynamicParameters();
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => typeof(SalesTransaction).GetProperty(parameter.Key)?.CanRead is true))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value))
            {
                continue;
            }

            var propertyInfo = typeof(SalesTransaction).GetProperty(parameter.Key);
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
        var items = result.Read<SalesTransaction>().ToList();
        var totalCount = result.ReadSingle<int>();

        return new PagedList<SalesTransaction>(items, pagedListRequest.PageNumber, pagedListRequest.PageSize, totalCount);
    }

    public async Task<IEnumerable<Period>> SelectPeriodsAsync(int companyId, CancellationToken cancellationToken)
    {
        const string query = "GiselSales_SelectPeriods";

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
