using System.Reflection;
using System.Text.RegularExpressions;
using GiselX.Common.Constants;
using GiselX.Domain.Common;
using GiselX.Repository;
using Microsoft.AspNetCore.Authorization;
using ApiSalesTransactionsController = GiselX.Web.Controllers.Api.SalesTransactionsController;
using ApiStocksController = GiselX.Web.Controllers.Api.StocksController;

namespace GiselX.Tests;

/// <summary>
/// Distributors see the same Stock and Sellout summary tiles as Administrators. The summary queries
/// once summed every company because only Administrators could reach them; opened to Distributors they
/// must be scoped to the caller's company, and must count exactly the rows the Distributor's grid shows.
/// </summary>
public sealed class SelectionSummaryScopeTests
{
    [Fact]
    public void Stock_summary_lets_company_zero_match_any_company_and_otherwise_scopes_to_it()
    {
        var body = MethodBody(File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "StockRepository.cs")),
            "SummarizeAsync");

        Assert.Contains("(@CompanyId=0 OR s.CompanyId=@CompanyId)", body, StringComparison.Ordinal);
        Assert.Contains("parameters.Add(\"CompanyId\", companyId)", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Stock_summary_splits_saldo_akhir_by_condition()
    {
        var body = MethodBody(File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", "StockRepository.cs")),
            "SummarizeAsync");

        Assert.Contains("CASE WHEN s.Condition=0", body, StringComparison.Ordinal);
        Assert.Contains("CASE WHEN s.Condition=1", body, StringComparison.Ordinal);
    }

    [Fact]
    public void Sellout_summary_for_a_distributor_is_limited_to_that_company()
    {
        var (sql, parameters) = SalesSelectionSummaryQuery.Build(Request(("Year", "2026"), ("Month", "10")), 7, true);

        Assert.Contains("(@CompanyId=0 OR st.CompanyId=@CompanyId)", sql, StringComparison.Ordinal);
        Assert.Equal(7, parameters.Get<int>("CompanyId"));
    }

    [Fact]
    public void Sellout_summary_for_an_administrator_spans_every_company()
    {
        var (_, parameters) = SalesSelectionSummaryQuery.Build(Request(("Year", "2026"), ("Month", "10")), 0, false);

        Assert.Equal(0, parameters.Get<int>("CompanyId"));
    }

    [Fact]
    public void Sellout_summary_column_filter_matches_the_distributor_grid_exactly()
    {
        // GiselSales_Select, which backs a Distributor's grid, matches a column filter with LIKE as typed.
        var (_, distributor) = SalesSelectionSummaryQuery.Build(Request(("CustomerName", "Toko A")), 7, true);
        var (_, administrator) = SalesSelectionSummaryQuery.Build(Request(("CustomerName", "Toko A")), 0, false);

        Assert.Equal("Toko A", distributor.Get<string>("CustomerName"));
        Assert.Equal("%Toko A%", administrator.Get<string>("CustomerName"));
    }

    [Fact]
    public void Sellout_summary_column_filter_keeps_a_typed_wildcard_for_a_distributor()
    {
        var (_, parameters) = SalesSelectionSummaryQuery.Build(Request(("CustomerName", "Toko*")), 7, true);

        Assert.Equal("Toko%", parameters.Get<string>("CustomerName"));
    }

    [Fact]
    public void Sellout_summary_search_ignores_the_company_name_for_a_distributor()
    {
        var distributor = Normalize(SalesSelectionSummaryQuery.Build(Search("abc"), 7, true).Sql);
        var administrator = Normalize(SalesSelectionSummaryQuery.Build(Search("abc"), 0, false).Sql);

        Assert.DoesNotContain("c.Name LIKE @Search", distributor, StringComparison.Ordinal);
        Assert.Contains("c.Name LIKE @Search", administrator, StringComparison.Ordinal);
    }

    [Fact]
    public void Sellout_summary_ignores_a_company_name_filter_from_a_distributor()
    {
        var sql = Normalize(SalesSelectionSummaryQuery.Build(Request(("CompanyName", "Other")), 7, true).Sql);

        Assert.DoesNotContain("@CompanyName", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Sellout_summary_keeps_every_row_of_the_period_like_the_grid()
    {
        var sql = Normalize(SalesSelectionSummaryQuery.Build(Request(("Year", "2026"), ("Month", "10")), 7, true).Sql);

        Assert.DoesNotContain("LatestCreatedDate", sql, StringComparison.Ordinal);
        Assert.Contains("YEAR(st.InvoiceDate)=@Year AND MONTH(st.InvoiceDate)=@Month", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(typeof(ApiStocksController), PermissionConstants.Stocks.Index)]
    [InlineData(typeof(ApiSalesTransactionsController), PermissionConstants.SalesTransactions.Index)]
    public void Summary_endpoints_need_the_page_permission_rather_than_the_administrator_role(Type controller,
        string permission)
    {
        var authorize = controller.GetMethod("SummaryAsync")!.GetCustomAttributes<AuthorizeAttribute>().Single();

        Assert.Equal(permission, authorize.Policy);
        Assert.Null(authorize.Roles);
    }

    [Theory]
    [InlineData("StocksController.cs", "_stockService")]
    [InlineData("SalesTransactionsController.cs", "_salesTransactionService")]
    public void Summary_endpoints_read_product_identity_the_way_the_grid_does(string file, string service)
    {
        var source = File.ReadAllText(FindFromSolutionRoot("GiselX.Web", "Controllers", "Api", file));

        Assert.Contains($"{service}.SelectSummaryAsync(listRequest, User.IsInRole(\"Distributor\"),", source,
            StringComparison.Ordinal);
    }

    private static PagedListRequest Request(params (string Key, string Value)[] filters) => new()
    {
        Filters = filters.ToDictionary(filter => filter.Key, filter => filter.Value)
    };

    private static PagedListRequest Search(string term) => new() { SearchTerm = term };

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static string MethodBody(string source, string method)
    {
        var start = source.IndexOf($"> {method}(", StringComparison.Ordinal);
        Assert.True(start >= 0, $"{method} not found.");
        var end = source.IndexOf("\n    }", start, StringComparison.Ordinal);
        return source[start..end];
    }

    private static string FindFromSolutionRoot(params string[] parts)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return Path.Combine(directory?.FullName ?? Directory.GetCurrentDirectory(), Path.Combine(parts));
    }
}
