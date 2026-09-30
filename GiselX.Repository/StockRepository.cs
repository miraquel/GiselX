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
        dataTable.Columns.Add(nameof(Stock.ProductPcsInCtn), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.ProductNetto), typeof(decimal));
        dataTable.Columns.Add(nameof(Stock.ProductUnit), typeof(string));
        dataTable.Columns.Add(nameof(Stock.SmallestUnit), typeof(string));
        dataTable.Columns.Add(nameof(Stock.SmallestUnitQty), typeof(decimal));
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
                (object?)stock.SmallestUnit ?? DBNull.Value,
                (object?)stock.SmallestUnitQty ?? DBNull.Value,
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
        if (companyId == 0)
            return await _dbConnection.QueryAsync<Stock>(new CommandDefinition("""
                SELECT s.*, c.Name AS CompanyName
                FROM Stock s INNER JOIN Company c ON c.Id=s.CompanyId
                WHERE YEAR(s.CreatedDate)=@Year AND MONTH(s.CreatedDate)=@Month
                ORDER BY c.Name, s.CreatedDate, s.Id;
                """, new { Year = year, Month = month }, _dbTransaction,
                cancellationToken: cancellationToken));

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

    public async Task<PagedList<Stock>> SelectAsync(PagedListRequest pagedListRequest, int companyId,
        bool useDistributorIdentity, CancellationToken cancellationToken)
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
            nameof(Stock.SmallestUnit), nameof(Stock.SmallestUnitQty), nameof(Stock.PriceUnit),
            nameof(Stock.SaldoAwal), nameof(Stock.SaldoMasukPO), nameof(Stock.SaldoAkhir),
            nameof(Stock.BatchNumber), nameof(Stock.ExpiredDate), nameof(Stock.StockDate),
            nameof(Stock.CompanyName)
        };
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => allowed.Contains(parameter.Key)))
        {
            if (string.IsNullOrWhiteSpace(parameter.Value)) continue;
            var propertyInfo = typeof(Stock).GetProperty(parameter.Key);
            if (propertyInfo is null) continue;
            switch (propertyInfo.PropertyType)
            {
                case { } t when t == typeof(string):
                    var stringExpression = parameter.Key == nameof(Stock.CompanyName)
                        ? "c.Name"
                        : GetColumnExpression(parameter.Key, useDistributorIdentity);
                    clauses.Add($"{stringExpression} LIKE @{parameter.Key}");
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
            var productId = GetColumnExpression(nameof(Stock.ProductId), useDistributorIdentity);
            var productName = GetColumnExpression(nameof(Stock.ProductName), useDistributorIdentity);
            clauses.Add($"({productId} LIKE @Search OR {productName} LIKE @Search OR s.BatchNumber LIKE @Search OR c.Name LIKE @Search)");
            parameters.Add("Search", $"%{pagedListRequest.SearchTerm}%");
        }
        var where = clauses.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", clauses);
        var sort = typeof(Stock).GetProperties()
            .FirstOrDefault(property => allowed.Contains(property.Name) &&
                                        string.Equals(property.Name, pagedListRequest.SortBy,
                                            StringComparison.OrdinalIgnoreCase))?.Name
            ?? nameof(Stock.ProductId);
        var sortExpression = sort == nameof(Stock.CompanyName)
            ? "c.Name"
            : GetColumnExpression(sort, useDistributorIdentity);
        var direction = pagedListRequest.IsSortAscending ? "ASC" : "DESC";
        var projection = GetProjection(useDistributorIdentity);
        var rankedBatches = StockSnapshotPeriod.RankedBatchesFor(pagedListRequest.Filters, parameters);
        var query = $"""
            {rankedBatches}
            SELECT {projection}
            FROM Stock s
            INNER JOIN RankedBatches rb ON rb.Id=s.StockUploadBatchId AND rb.BatchRank=1
            INNER JOIN Company c ON c.Id=s.CompanyId
            WHERE (@CompanyId=0 OR s.CompanyId=@CompanyId) {where}
             ORDER BY {sortExpression} {direction}, s.Id ASC
             OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
            {rankedBatches}
            SELECT COUNT(*)
            FROM Stock s
            INNER JOIN RankedBatches rb ON rb.Id=s.StockUploadBatchId AND rb.BatchRank=1
            INNER JOIN Company c ON c.Id=s.CompanyId
            WHERE (@CompanyId=0 OR s.CompanyId=@CompanyId) {where};
            """;
        using var result = await _dbConnection.QueryMultipleAsync(new CommandDefinition(query, parameters,
            _dbTransaction, cancellationToken: cancellationToken));
        var items = (await result.ReadAsync<Stock>()).ToList();
        var totalCount = await result.ReadSingleAsync<int>();
        return new PagedList<Stock>(items, Math.Max(1, pagedListRequest.PageNumber),
            Math.Clamp(pagedListRequest.PageSize, 1, 500), totalCount);
    }

    public Task<StockSelectionSummary> SummarizeAsync(PagedListRequest request, bool useDistributorIdentity,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        var clauses = new List<string>();
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(Stock.ProductId), nameof(Stock.ProductName), nameof(Stock.ProductPackaging),
            nameof(Stock.ProductPcsInCtn), nameof(Stock.ProductNetto), nameof(Stock.ProductUnit),
            nameof(Stock.SmallestUnit), nameof(Stock.SmallestUnitQty),
            nameof(Stock.PriceUnit), nameof(Stock.SaldoAwal), nameof(Stock.SaldoMasukPO),
            nameof(Stock.SaldoAkhir), nameof(Stock.BatchNumber), nameof(Stock.ExpiredDate),
            nameof(Stock.StockDate), nameof(Stock.CompanyName)
        };
        foreach (var filter in request.Filters.Where(filter => allowed.Contains(filter.Key)))
        {
            if (string.IsNullOrWhiteSpace(filter.Value)) continue;
            var property = typeof(Stock).GetProperty(filter.Key);
            if (property is null) continue;
            var expression = filter.Key == nameof(Stock.CompanyName)
                ? "c.Name" : GetColumnExpression(filter.Key, useDistributorIdentity);
            switch (property.PropertyType)
            {
                case { } type when type == typeof(string):
                    var text = filter.Value.Replace('*', '%');
                    parameters.Add(filter.Key, text.Contains('%') ? text : $"%{text}%");
                    clauses.Add($"{expression} LIKE @{filter.Key}");
                    break;
                case { } type when type == typeof(int):
                    if (int.TryParse(filter.Value, out var integer))
                    { parameters.Add(filter.Key, integer); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(decimal) || type == typeof(decimal?):
                    if (decimal.TryParse(filter.Value, out var number))
                    { parameters.Add(filter.Key, number); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(DateTime) || type == typeof(DateTime?):
                    if (DateTime.TryParse(filter.Value, out var date))
                    { parameters.Add(filter.Key, date); clauses.Add($"CONVERT(date,{expression})=CONVERT(date,@{filter.Key})"); }
                    break;
            }
        }
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Replace('*', '%');
            parameters.Add("Search", search.Contains('%') ? search : $"%{search}%");
            clauses.Add($"({GetColumnExpression(nameof(Stock.ProductId), useDistributorIdentity)} LIKE @Search OR " +
                        $"{GetColumnExpression(nameof(Stock.ProductName), useDistributorIdentity)} LIKE @Search OR " +
                        "s.BatchNumber LIKE @Search OR c.Name LIKE @Search)");
        }
        var filters = clauses.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", clauses);
        var rankedBatches = StockSnapshotPeriod.RankedBatchesFor(request.Filters, parameters);
        return _dbConnection.QuerySingleAsync<StockSelectionSummary>(new CommandDefinition($"""
            {rankedBatches}
            SELECT COUNT(DISTINCT s.CompanyId) AS DistributorCount,
                   COUNT(*) AS [RowCount],
                   COALESCE(SUM(s.SaldoAwal),0) AS SaldoAwal,
                   COALESCE(SUM(s.SaldoMasukPO),0) AS SaldoMasukPO,
                   COALESCE(SUM(s.SaldoAkhir),0) AS SaldoAkhir,
                   COALESCE(SUM(COALESCE(s.SaldoAkhir,0) * COALESCE(s.PriceUnit,0)),0) AS StockValue
            FROM Stock s
            INNER JOIN RankedBatches rb ON rb.Id=s.StockUploadBatchId AND rb.BatchRank=1
            INNER JOIN Company c ON c.Id=s.CompanyId
            WHERE 1=1 {filters};
            """, parameters, _dbTransaction, cancellationToken: cancellationToken));
    }

    /// <summary>
    /// The months the Stock page can be browsed by: the snapshot dates of published batches. Upload
    /// time would offer a month for a snapshot uploaded late and hide the month it describes, and
    /// legacy rows with no batch cannot be shown by the grid at all.
    /// </summary>
    public async Task<IEnumerable<Period>> SelectPeriodsAsync(int companyId, CancellationToken cancellationToken)
    {
        return await _dbConnection.QueryAsync<Period>(new CommandDefinition("""
            SELECT DISTINCT YEAR(StockDate) AS [Year], MONTH(StockDate) AS [Month]
            FROM StockUploadBatch
            WHERE Status IN (2,3) AND (@CompanyId=0 OR CompanyId=@CompanyId)
            ORDER BY [Year] DESC, [Month] DESC;
            """, new { CompanyId = companyId }, _dbTransaction, cancellationToken: cancellationToken));
    }

    public async Task<IReadOnlyList<StockUploadBatch>> ListCompletedUploadsAsync(int companyId,
        CancellationToken cancellationToken) => (await _dbConnection.QueryAsync<StockUploadBatch>(new CommandDefinition("""
            SELECT b.*, c.Name AS CompanyName
            FROM StockUploadBatch b INNER JOIN Company c ON c.Id=b.CompanyId
            WHERE (@CompanyId=0 OR b.CompanyId=@CompanyId) AND b.Status IN (2,3)
            ORDER BY b.UploadedDate DESC, b.Id DESC
            """, new { CompanyId = companyId }, _dbTransaction, cancellationToken: cancellationToken))).ToList();

    public Task<IEnumerable<Stock>> SelectByBatchAsync(long batchId, int companyId, bool useDistributorIdentity,
        CancellationToken cancellationToken)
    {
        var projection = GetProjection(useDistributorIdentity);
        var productId = GetColumnExpression(nameof(Stock.ProductId), useDistributorIdentity);
        return _dbConnection.QueryAsync<Stock>(new CommandDefinition($"""
            SELECT {projection} FROM Stock s
            INNER JOIN StockUploadBatch b ON b.Id=s.StockUploadBatchId
            INNER JOIN Company c ON c.Id=b.CompanyId
            WHERE b.Id=@BatchId AND (@CompanyId=0 OR b.CompanyId=@CompanyId) AND b.Status IN (2,3)
            ORDER BY {productId}, s.Id
            """, new { BatchId = batchId, CompanyId = companyId }, _dbTransaction,
            cancellationToken: cancellationToken));
    }

    private static string GetColumnExpression(string field, bool useDistributorIdentity) => field switch
    {
        nameof(Stock.ProductId) when useDistributorIdentity => "COALESCE(NULLIF(s.DistributorProductId, ''), s.ProductId)",
        nameof(Stock.ProductName) when useDistributorIdentity => "COALESCE(NULLIF(s.DistributorProductName, ''), s.ProductName)",
        _ => $"s.[{field}]"
    };

    private static string GetProjection(bool useDistributorIdentity)
    {
        var productId = GetColumnExpression(nameof(Stock.ProductId), useDistributorIdentity);
        var productName = GetColumnExpression(nameof(Stock.ProductName), useDistributorIdentity);
        return $"""
            s.Id,
            {productId} AS ProductId,
            {productName} AS ProductName,
            s.DistributorProductId,
            s.DistributorProductName,
            s.ItemMappingId,
            s.ProductPackaging,
            s.ProductPcsInCtn,
            s.ProductNetto,
            s.ProductUnit,
            s.SmallestUnit,
            s.SmallestUnitQty,
            s.PriceUnit,
            s.SaldoAwal,
            s.SaldoMasukPO,
            s.SaldoAkhir,
            s.BatchNumber,
            s.ExpiredDate,
            s.CompanyId,
            s.CreatedDate,
            s.StockUploadBatchId,
            s.StockDate,
            c.Name AS CompanyName
            """;
    }
}
