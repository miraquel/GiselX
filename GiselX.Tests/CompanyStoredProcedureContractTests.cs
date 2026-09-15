using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// The .sql files under GiselX.Web/StoredProcedures are the readable source of each procedure, and
/// migrations are written from them — but nothing deploys them, so they can drift from what the
/// database actually holds and nobody notices until a migration reinstalls the stale text.
///
/// That happened: the Company files still described the pre-Channel/Salesman signature long after
/// 20260904030000_CreateChannelAndSalesmanMaster had deployed the newer one. Regenerating a
/// migration from them dropped @ChannelId and @SalesmanId, and every Company save failed with
/// "Procedure or function Company_Update has too many arguments specified."
///
/// These tests pin the files to what CompanyRepository actually sends.
/// </summary>
public sealed class CompanyStoredProcedureContractTests
{
    [Theory]
    [InlineData("CreateCompanyAsync", "Company_Insert")]
    [InlineData("UpdateCompanyAsync", "Company_Update")]
    public void Every_parameter_the_repository_sends_is_declared_by_the_procedure(
        string repositoryMethod, string procedure)
    {
        var sent = ParametersSentBy(repositoryMethod);
        Assert.NotEmpty(sent);

        var declared = ParametersDeclaredBy(procedure);

        var missing = sent.Where(name => !declared.Contains(name, StringComparer.OrdinalIgnoreCase)).ToList();
        Assert.True(missing.Count == 0,
            $"{procedure}.sql does not declare {string.Join(", ", missing)}, which " +
            $"CompanyRepository.{repositoryMethod} sends. Calling it would fail with " +
            "\"too many arguments specified\".");
    }


    [Theory]
    [InlineData("Company_Insert")]
    [InlineData("Company_Update")]
    [InlineData("Company_SelectById")]
    [InlineData("Company_Select")]
    public void The_procedure_still_knows_about_channel_and_salesman(string procedure)
    {
        var sql = ReadProcedure(procedure);

        Assert.Contains("Channel", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Salesman", sql, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Reads the @Parameters a repository method hands to Dapper, in source order.</summary>
    private static List<string> ParametersSentBy(string methodName)
    {
        var source = File.ReadAllText(Path.Combine(RepositoryRoot(), "GiselX.Repository", "CompanyRepository.cs"));
        var start = source.IndexOf($"public async Task<Company> {methodName}", StringComparison.Ordinal);
        Assert.True(start >= 0, $"CompanyRepository.{methodName} was not found.");

        // Stop at the next method so a later method's parameters are not counted as this one's.
        var next = source.IndexOf("\n    public ", start + 1, StringComparison.Ordinal);
        var body = next > start ? source[start..next] : source[start..];

        return Regex.Matches(body, @"parameters\.Add\(""(@\w+)""")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> ParametersDeclaredBy(string procedure) =>
        Regex.Matches(ReadProcedure(procedure), @"(@\w+)\s+(?:INT|NVARCHAR|BIT|DATE|DECIMAL)")
            .Select(match => match.Groups[1].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static string ReadProcedure(string procedure) =>
        File.ReadAllText(Path.Combine(RepositoryRoot(), "GiselX.Web", "StoredProcedures", $"{procedure}.sql"));

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("GiselX.sln was not found.");
    }
}
