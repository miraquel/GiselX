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
        dataTable.Columns.Add(nameof(Stock.PriceUnit), typeof(decimal));
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
                (object?)stock.PriceUnit ?? DBNull.Value,
                stock.SaldoAwal,
                stock.SaldoMasukPO,
                stock.SaldoAkhir,
                stock.BatchNumber,
                stock.ExpiredDate,
                stock.CompanyId,
                stock.CreatedDate
            );
        }

        foreach (DataColumn column in dataTable.Columns)
        {
            sqlBulkCopy.ColumnMappings.Add(column.ColumnName, column.ColumnName);
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
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", companyId);
        parameters.Add("PageNumber", Math.Max(1, pagedListRequest.PageNumber));
        parameters.Add("PageSize", Math.Clamp(pagedListRequest.PageSize, 1, 500));
        var clauses = new List<string>();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Stock.ProductId), nameof(Stock.ProductName), nameof(Stock.ProductPackaging),
            nameof(Stock.ProductPcsInCtn), nameof(Stock.ProductNetto), nameof(Stock.ProductUnit),
            nameof(Stock.PriceUnit),
            nameof(Stock.SaldoAwal), nameof(Stock.SaldoMasukPO), nameof(Stock.SaldoAkhir),
            nameof(Stock.BatchNumber), nameof(Stock.ExpiredDate), nameof(Stock.StockDate)
        };
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => allowed.Contains(parameter.Key)))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value)) continue;
            var propertyInfo = typeof(Stock).GetProperty(parameter.Key);
            if (propertyInfo is null) continue;
            switch (propertyInfo.PropertyType)
            {
                case { } t when t == typeof(string):
                    clauses.Add($"s.[{parameter.Key}] LIKE @{parameter.Key}");
                    var text = parameter.Value.Replace('*', '%');
                    parameters.Add(parameter.Key, text.Contains('%') ? text : $"%{text}%");
                    break;
                case { } t when t == typeof(int):
                    if (int.TryParse(parameter.Value, out var intValue))
                    { clauses.Add($"s.[{parameter.Key}] = @{parameter.Key}"); parameters.Add(parameter.Key, intValue); }
                    break;
                case { } t when t == typeof(decimal) || t == typeof(decimal?):
                    if (decimal.TryParse(parameter.Value, out var decimalValue))
                    { clauses.Add($"s.[{parameter.Key}] = @{parameter.Key}"); parameters.Add(parameter.Key, decimalValue); }
                    break;
                case { } t when t == typeof(DateTime) || t == typeof(DateTime?):
                    if (DateTime.TryParse(parameter.Value, out var dateTimeValue) ||
                        DateTime.TryParseExact(parameter.Value, "yyyy-MM-dd", null, System.Globalization.DateTimeStyles.None, out dateTimeValue))
                    { clauses.Add($"CONVERT(date,s.[{parameter.Key}]) = CONVERT(date,@{parameter.Key})"); parameters.Add(parameter.Key, dateTimeValue); }
                    break;
            }
        }
        if (!string.IsNullOrWhiteSpace(pagedListRequest.SearchTerm))
        {
            clauses.Add("(s.ProductId LIKE @Search OR s.ProductName LIKE @Search OR s.BatchNumber LIKE @Search)");
            parameters.Add("Search", $"%{pagedListRequest.SearchTerm}%");
        }
        var where = clauses.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", clauses);
        var sort = allowed.Contains(pagedListRequest.SortBy) ? pagedListRequest.SortBy : nameof(Stock.ProductId);
        var direction = pagedListRequest.IsSortAscending ? "ASC" : "DESC";
        var query = $"""
            DECLARE @BatchId bigint = (SELECT TOP (1) Id FROM StockUploadBatch
             WHERE CompanyId=@CompanyId AND Status IN (2,3)
             ORDER BY CompletedDate DESC, Id DESC);
            SELECT s.* FROM Stock s WHERE s.CompanyId=@CompanyId AND s.StockUploadBatchId=@BatchId {where}
             ORDER BY s.[{sort}] {direction}, s.Id ASC
             OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
            SELECT COUNT(*) FROM Stock s WHERE s.CompanyId=@CompanyId AND s.StockUploadBatchId=@BatchId {where};
            """;
        using var result = await _dbConnection.QueryMultipleAsync(new CommandDefinition(query, parameters,
            _dbTransaction, cancellationToken: cancellationToken));
        var items = (await result.ReadAsync<Stock>()).ToList();
        var totalCount = await result.ReadSingleAsync<int>();
        return new PagedList<Stock>(items, Math.Max(1, pagedListRequest.PageNumber),
            Math.Clamp(pagedListRequest.PageSize, 1, 500), totalCount);
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

    public async Task<IReadOnlyList<StockUploadBatch>> ListCompletedUploadsAsync(int companyId,
        CancellationToken cancellationToken) => (await _dbConnection.QueryAsync<StockUploadBatch>(new CommandDefinition("""
            SELECT * FROM StockUploadBatch WHERE CompanyId=@CompanyId AND Status IN (2,3)
            ORDER BY UploadedDate DESC, Id DESC
            """, new { CompanyId = companyId }, _dbTransaction, cancellationToken: cancellationToken))).ToList();

    public Task<IEnumerable<Stock>> SelectByBatchAsync(long batchId, int companyId,
        CancellationToken cancellationToken) => _dbConnection.QueryAsync<Stock>(new CommandDefinition("""
            SELECT s.* FROM Stock s INNER JOIN StockUploadBatch b ON b.Id=s.StockUploadBatchId
            WHERE b.Id=@BatchId AND b.CompanyId=@CompanyId AND b.Status IN (2,3)
            ORDER BY s.ProductId, s.Id
            """, new { BatchId = batchId, CompanyId = companyId }, _dbTransaction,
            cancellationToken: cancellationToken));
}
