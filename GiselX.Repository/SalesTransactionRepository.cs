using System.Data;
using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using Microsoft.Data.SqlClient;

namespace GiselX.Repository;

public class SalesTransactionRepository : ISalesTransactionRepository
{
    internal static readonly HashSet<string> SelectableFields =
    [
        nameof(SalesTransaction.CustomerId), nameof(SalesTransaction.CustomerName),
        nameof(SalesTransaction.CustomerAlias), nameof(SalesTransaction.CustomerAddress),
        nameof(SalesTransaction.ProductId), nameof(SalesTransaction.ProductBrand),
        nameof(SalesTransaction.ProductName), nameof(SalesTransaction.ProductPackaging),
        nameof(SalesTransaction.BatchNumber), nameof(SalesTransaction.InvoiceNo),
        nameof(SalesTransaction.InvoiceUnit), nameof(SalesTransaction.GeisaPOId),
        nameof(SalesTransaction.SalesmanNameGMK), nameof(SalesTransaction.ProductPcsInCtn),
        nameof(SalesTransaction.InvoiceQty), nameof(SalesTransaction.UnitPrice),
        nameof(SalesTransaction.GrossValue), nameof(SalesTransaction.DiscountValue),
        nameof(SalesTransaction.DiscountPct), nameof(SalesTransaction.NetValue), nameof(SalesTransaction.InKg),
        nameof(SalesTransaction.ExpiredDate), nameof(SalesTransaction.InvoiceDate),
        nameof(SalesTransaction.ShipDate), nameof(SalesTransaction.CreatedDate),
        nameof(SalesTransaction.CompanyName)
    ];
    internal static readonly HashSet<string> NormalSelectableFields = SelectableFields
        .Where(field => field != nameof(SalesTransaction.CompanyName)).ToHashSet();

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
        dataTable.Columns.Add(nameof(SalesTransaction.ProductPcsInCtn), typeof(decimal));
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

    public async Task<IEnumerable<SalesTransaction>> SelectByCustPeriodAsync(int companyId, int year, int month,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        if (companyId == 0)
            return await SelectAllByPeriodAsync(year, month, useDistributorIdentity, cancellationToken);

        const string query = "GiselSales_SelectByCustPeriod";

        var parameters = new DynamicParameters();
        parameters.Add("@CompanyId", companyId);
        parameters.Add("@Year", year);
        parameters.Add("@Month", month);
        parameters.Add("@UseDistributorIdentity", useDistributorIdentity);

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

    public async Task<PagedList<SalesTransaction>> SelectAsync(PagedListRequest pagedListRequest, int companyId,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        if (companyId == 0)
            return await SelectAllAsync(pagedListRequest, useDistributorIdentity, cancellationToken);

        const string query = "GiselSales_Select";

        var parameters = new DynamicParameters();
        foreach (var parameter in pagedListRequest.Filters.Where(parameter => NormalSelectableFields.Contains(parameter.Key)))
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
        parameters.Add("@UseDistributorIdentity", useDistributorIdentity);

        if (!string.IsNullOrWhiteSpace(pagedListRequest.SearchTerm))
        {
            var searchTerm = pagedListRequest.SearchTerm.Replace('*', '%');
            parameters.Add("@SearchTerm", searchTerm.Contains('%') ? searchTerm : $"%{searchTerm}%");
        }

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
        if (companyId == 0)
            return await _dbConnection.QueryAsync<Period>(new CommandDefinition("""
                SELECT DISTINCT YEAR(InvoiceDate) AS [Year], MONTH(InvoiceDate) AS [Month]
                FROM SalesTransaction
                ORDER BY [Year] DESC, [Month] DESC;
                """, transaction: _dbTransaction, cancellationToken: cancellationToken));

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

    public Task<SalesSelectionSummary> SummarizeAsync(PagedListRequest request, int companyId,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var (sql, parameters) = SalesSelectionSummaryQuery.Build(request, companyId, useDistributorIdentity);
        return _dbConnection.QuerySingleAsync<SalesSelectionSummary>(new CommandDefinition(sql, parameters,
            _dbTransaction, cancellationToken: cancellationToken));
    }

    private async Task<PagedList<SalesTransaction>> SelectAllAsync(PagedListRequest request,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters();
        parameters.Add("PageNumber", Math.Max(1, request.PageNumber));
        parameters.Add("PageSize", Math.Clamp(request.PageSize, 1, 500));
        var clauses = new List<string>();
        var periodClauses = new List<string>();

        if (request.Filters.TryGetValue("Year", out var yearText) && int.TryParse(yearText, out var year))
        {
            parameters.Add("Year", year);
            periodClauses.Add("YEAR(st.InvoiceDate)=@Year");
        }
        if (request.Filters.TryGetValue("Month", out var monthText) && int.TryParse(monthText, out var month))
        {
            parameters.Add("Month", month);
            periodClauses.Add("MONTH(st.InvoiceDate)=@Month");
        }

        foreach (var filter in request.Filters.Where(filter => SelectableFields.Contains(filter.Key)))
        {
            if (string.IsNullOrWhiteSpace(filter.Value)) continue;
            var property = typeof(SalesTransaction).GetProperty(filter.Key);
            if (property is null) continue;
            var expression = GetAdministratorColumnExpression(filter.Key, useDistributorIdentity);
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
            clauses.Add($"({GetAdministratorColumnExpression(nameof(SalesTransaction.ProductId), useDistributorIdentity)} LIKE @Search OR " +
                        $"{GetAdministratorColumnExpression(nameof(SalesTransaction.ProductName), useDistributorIdentity)} LIKE @Search OR c.Name LIKE @Search)");
        }

        var filters = clauses.Count == 0 ? string.Empty : " AND " + string.Join(" AND ", clauses);
        var periodFilter = periodClauses.Count == 0 ? string.Empty : " WHERE " + string.Join(" AND ", periodClauses);
        var sort = typeof(SalesTransaction).GetProperties()
            .FirstOrDefault(property => SelectableFields.Contains(property.Name) &&
                                        string.Equals(property.Name, request.SortBy,
                                            StringComparison.OrdinalIgnoreCase))?.Name
            ?? nameof(SalesTransaction.InvoiceDate);
        var sortExpression = GetAdministratorColumnExpression(sort, useDistributorIdentity);
        var direction = request.IsSortAscending ? "ASC" : "DESC";
        var projection = GetAdministratorProjection(useDistributorIdentity);
        var query = $"""
            WITH LatestUploads AS (
                SELECT st.*, MAX(st.CreatedDate) OVER (PARTITION BY CompanyId) AS LatestCreatedDate
                FROM SalesTransaction st
                {periodFilter}
            )
            SELECT {projection}
            FROM LatestUploads st INNER JOIN Company c ON c.Id=st.CompanyId
            WHERE st.CreatedDate=st.LatestCreatedDate {filters}
            ORDER BY {sortExpression} {direction}, st.Id
            OFFSET (@PageNumber-1)*@PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;
            WITH LatestUploads AS (
                SELECT st.*, MAX(st.CreatedDate) OVER (PARTITION BY CompanyId) AS LatestCreatedDate
                FROM SalesTransaction st
                {periodFilter}
            )
            SELECT COUNT(*)
            FROM LatestUploads st INNER JOIN Company c ON c.Id=st.CompanyId
            WHERE st.CreatedDate=st.LatestCreatedDate {filters};
            """;
        using var result = await _dbConnection.QueryMultipleAsync(new CommandDefinition(query, parameters,
            _dbTransaction, cancellationToken: cancellationToken));
        var items = (await result.ReadAsync<SalesTransaction>()).ToList();
        var totalCount = await result.ReadSingleAsync<int>();
        return new PagedList<SalesTransaction>(items, Math.Max(1, request.PageNumber),
            Math.Clamp(request.PageSize, 1, 500), totalCount);
    }

    private Task<IEnumerable<SalesTransaction>> SelectAllByPeriodAsync(int year, int month,
        bool useDistributorIdentity, CancellationToken cancellationToken)
    {
        var projection = GetAdministratorProjection(useDistributorIdentity);
        return _dbConnection.QueryAsync<SalesTransaction>(new CommandDefinition($"""
            WITH LatestUploads AS (
                SELECT st.*, MAX(st.CreatedDate) OVER (PARTITION BY CompanyId) AS LatestCreatedDate
                FROM SalesTransaction st
                WHERE YEAR(st.InvoiceDate)=@Year AND MONTH(st.InvoiceDate)=@Month
            )
            SELECT {projection}
            FROM LatestUploads st INNER JOIN Company c ON c.Id=st.CompanyId
            WHERE st.CreatedDate=st.LatestCreatedDate
            ORDER BY c.Name, st.InvoiceDate, st.InvoiceNo, st.Id;
            """, new { Year = year, Month = month }, _dbTransaction, cancellationToken: cancellationToken));
    }

    internal static string GetAdministratorColumnExpression(string field, bool useDistributorIdentity) => field switch
    {
        nameof(SalesTransaction.CompanyName) => "c.Name",
        nameof(SalesTransaction.ProductId) when useDistributorIdentity =>
            "COALESCE(NULLIF(st.DistributorProductId,''),st.ProductId)",
        nameof(SalesTransaction.ProductName) when useDistributorIdentity =>
            "COALESCE(NULLIF(st.DistributorProductName,''),st.ProductName)",
        _ => $"st.[{field}]"
    };

    private static string GetAdministratorProjection(bool useDistributorIdentity) => $"""
        st.Id, st.CustomerId, st.CustomerName, st.CustomerAlias, st.CustomerAddress,
        {GetAdministratorColumnExpression(nameof(SalesTransaction.ProductId), useDistributorIdentity)} AS ProductId,
        st.ProductBrand,
        {GetAdministratorColumnExpression(nameof(SalesTransaction.ProductName), useDistributorIdentity)} AS ProductName,
        st.ProductPackaging, st.ProductPcsInCtn, st.BatchNumber, st.ExpiredDate, st.InvoiceNo,
        st.InvoiceDate, st.InvoiceQty, st.InvoiceUnit, st.UnitPrice, st.GrossValue, st.DiscountValue,
        st.DiscountPct, st.NetValue, st.InKg, st.GeisaPOId, st.ShipDate, st.SalesmanNameGMK,
        st.CompanyId, st.CreatedDate, c.Name AS CompanyName
        """;
}
