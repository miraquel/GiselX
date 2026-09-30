using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// The internal notification mail sends PT. GMK staff the same result and source-file links the
/// distributor gets. Those staff are Administrators under their own company, so the lookups behind the
/// links must accept 0 as "any company" — scoped strictly, every internal link returned not found.
/// </summary>
public sealed class UploadResultAdministratorScopeTests
{
    [Theory]
    [InlineData("StockUploadRepository.cs", "GetBatchAsync", 2)]
    [InlineData("SelloutUploadRepository.cs", "GetBatchAsync", 2)]
    [InlineData("SelloutUploadRepository.cs", "GetGroupAsync", 3)]
    public void Result_lookups_let_company_zero_match_any_company(string file, string method, int statements)
    {
        var body = MethodBody(File.ReadAllText(FindFromSolutionRoot("GiselX.Repository", file)), method);

        Assert.Equal(statements, Regex.Matches(body, @"\(@CompanyId=0 OR [a-z]\.CompanyId=@CompanyId\)").Count);
        Assert.DoesNotMatch(@"AND [a-z]\.CompanyId=@CompanyId", body);
    }

    [Theory]
    [InlineData("StocksController.cs")]
    [InlineData("SalesTransactionsController.cs")]
    public void Controllers_pass_administrators_the_any_company_scope(string file)
    {
        var source = File.ReadAllText(FindFromSolutionRoot("GiselX.Web", "Controllers", file));

        Assert.Contains("User.IsInRole(\"Administrator\") ? 0 : user.CompanyId", source, StringComparison.Ordinal);
        Assert.Equal(2, Regex.Matches(source, @"UploadScope\(user\)").Count);
    }

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
