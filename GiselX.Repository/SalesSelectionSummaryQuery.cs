using Dapper;
using GiselX.Domain;
using GiselX.Domain.Common;

namespace GiselX.Repository;

/// <summary>
/// The Sellout summary tiles must total exactly the rows the grid beside them shows. An Administrator's
/// grid is the inline query in <see cref="SalesTransactionRepository"/>; a Distributor's is
/// GiselSales_Select, which matches a column filter with LIKE as typed, searches product only and cannot
/// filter by company name. A company of 0 is any company; anything else is that company and the
/// Distributor grid's matching rules.
/// </summary>
public static class SalesSelectionSummaryQuery
{
    public static (string Sql, DynamicParameters Parameters) Build(PagedListRequest request, int companyId,
        bool useDistributorIdentity)
    {
        var distributorScope = companyId != 0;
        var parameters = new DynamicParameters();
        parameters.Add("CompanyId", companyId);
        var clauses = new List<string>();
        var periodClauses = new List<string> { "(@CompanyId=0 OR st.CompanyId=@CompanyId)" };
        if (request.Filters.TryGetValue("Year", out var yearText) && int.TryParse(yearText, out var year))
        { parameters.Add("Year", year); periodClauses.Add("YEAR(st.InvoiceDate)=@Year"); }
        if (request.Filters.TryGetValue("Month", out var monthText) && int.TryParse(monthText, out var month))
        { parameters.Add("Month", month); periodClauses.Add("MONTH(st.InvoiceDate)=@Month"); }

        var fields = distributorScope
            ? SalesTransactionRepository.NormalSelectableFields
            : SalesTransactionRepository.SelectableFields;
        foreach (var filter in request.Filters.Where(filter => fields.Contains(filter.Key)))
        {
            if (string.IsNullOrWhiteSpace(filter.Value)) continue;
            var property = typeof(SalesTransaction).GetProperty(filter.Key);
            if (property is null) continue;
            var expression = SalesTransactionRepository.GetAdministratorColumnExpression(filter.Key, useDistributorIdentity);
            switch (property.PropertyType)
            {
                case { } type when type == typeof(string):
                    var text = filter.Value.Replace('*', '%');
                    var hasWildcard = text.Contains('%');
                    parameters.Add(filter.Key, hasWildcard || distributorScope ? text : $"%{text}%");
                    clauses.Add($"{expression} LIKE @{filter.Key}");
                    break;
                case { } type when type == typeof(int):
                    if (int.TryParse(filter.Value, out var integer))
                    { parameters.Add(filter.Key, integer); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(decimal):
                    if (decimal.TryParse(filter.Value, out var number))
                    { parameters.Add(filter.Key, number); clauses.Add($"{expression}=@{filter.Key}"); }
                    break;
                case { } type when type == typeof(DateTime):
                    if (DateTime.TryParse(filter.Value, out var date))
                    { parameters.Add(filter.Key, date); clauses.Add($"CONVERT(date,{expression})=CONVERT(date,@{filter.Key})"); }
                    break;
            }
        }
        if (!string.IsNullOrWhiteSpace(request.SearchTerm))
        {
            var search = request.SearchTerm.Replace('*', '%');
            parameters.Add("Search", search.Contains('%') ? search : $"%{search}%");
            var productId = SalesTransactionRepository.GetAdministratorColumnExpression(
                nameof(SalesTransaction.ProductId), useDistributorIdentity);
            var productName = SalesTransactionRepository.GetAdministratorColumnExpression(
                nameof(SalesTransaction.ProductName), useDistributorIdentity);
            clauses.Add(distributorScope
                ? $"({productId} LIKE @Search OR {productName} LIKE @Search)"
                : $"({productId} LIKE @Search OR {productName} LIKE @Search OR c.Name LIKE @Search)");
        }

        var where = "WHERE " + string.Join(" AND ", periodClauses.Concat(clauses));
        return ($"""
            SELECT COUNT(DISTINCT st.CompanyId) AS DistributorCount,
                   COUNT(*) AS [RowCount],
                   COALESCE(SUM(st.InvoiceQty),0) AS InvoiceQty,
                   COALESCE(SUM(COALESCE(st.GrossValue, st.InvoiceQty * st.UnitPrice)),0) AS GrossValue,
                   COALESCE(SUM(st.DiscountValue),0) AS DiscountValue,
                   COALESCE(SUM(COALESCE(st.NetValue,
                       COALESCE(st.GrossValue, st.InvoiceQty * st.UnitPrice) - COALESCE(st.DiscountValue,0))),0) AS NetValue
            FROM SalesTransaction st INNER JOIN Company c ON c.Id=st.CompanyId
            {where};
            """, parameters);
    }
}
