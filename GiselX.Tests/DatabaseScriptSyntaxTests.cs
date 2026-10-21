using System.Text.RegularExpressions;

namespace GiselX.Tests;

/// <summary>
/// These scripts are the only way a schema change reaches a server that does not run
/// `dotnet ef database update`, and nothing executes them before a deployment does.
///
/// One of them shipped with every string literal stripped of its quotes — N'permission' had become
/// Npermission — because it was assembled through a shell and then only its EF migration was tested,
/// not the script itself. SQL Server read the literals as column names and the whole install failed
/// on the server. These assertions catch that class of damage in the suite instead.
/// </summary>
public sealed class DatabaseScriptSyntaxTests
{
    public static TheoryData<string> AllScripts()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.GetFiles(ScriptFolder(), "*.sql"))
        {
            data.Add(Path.GetFileName(path));
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(AllScripts))]
    public void Permission_claim_comparisons_use_quoted_literals(string fileName)
    {
        // This is exactly what broke: N'permission' became Npermission, and SQL Server read it as a
        // column name. Comparisons against ClaimType and ClaimValue are always string literals —
        // never a bare word — which makes this narrow enough to never cry wolf, unlike a general
        // hunt for N followed by a word, which flags ordinary columns like Name and NetValue.
        var sql = StripComments(ReadScript(fileName));

        // Matches only when what follows '=' is a bare word rather than a quoted literal. A column
        // reference (alias.ClaimValue) is excluded — those are legitimate joins.
        var unquoted = Regex.Matches(sql, @"\b(ClaimType|ClaimValue)\s*=\s*(?!N?')([A-Za-z_][A-Za-z0-9_.]*)")
            // A dot means a column reference such as g.Value — comparing two columns is legitimate.
            .Where(match => !match.Groups[2].Value.Contains('.'))
            .Select(match => match.Value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        Assert.True(unquoted.Count == 0,
            $"{fileName} compares a claim against an unquoted value: {string.Join("; ", unquoted)}. " +
            "An N'...' literal has lost its quotes and the script will fail on the server.");
    }

    [Theory]
    [MemberData(nameof(AllScripts))]
    public void Quotes_are_balanced(string fileName)
    {
        // An odd number of apostrophes outside comments means a literal is not closed, which is the
        // other way shell assembly breaks a script.
        var sql = StripComments(ReadScript(fileName));

        Assert.True(sql.Count(character => character == '\'') % 2 == 0,
            $"{fileName} has an unbalanced number of apostrophes; a string literal is not closed.");
    }

    [Theory]
    [MemberData(nameof(AllScripts))]
    public void The_identity_schema_is_always_bracketed(string fileName)
    {
        // "identity" is the T-SQL IDENTITY keyword and will not parse bare. This has broken a
        // deployment once already.
        var sql = StripComments(ReadScript(fileName));

        Assert.DoesNotMatch(@"(?<!\[)\bidentity\.\[?AspNet", sql);
    }

    /// <summary>
    /// Removes both comment forms. The headers in these files are prose and contain apostrophes —
    /// "the batch's rows" — which are not string literals and would unbalance the count.
    /// </summary>
    private static string StripComments(string sql)
    {
        var withoutBlocks = Regex.Replace(sql, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return string.Join('\n', withoutBlocks.Split('\n')
            .Where(line => !line.TrimStart().StartsWith("--")));
    }

    private static string ReadScript(string fileName) =>
        File.ReadAllText(Path.Combine(ScriptFolder(), fileName));

    private static string ScriptFolder() =>
        Path.Combine(RepositoryRoot(), "GiselX.Web", "DatabaseScripts");

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
