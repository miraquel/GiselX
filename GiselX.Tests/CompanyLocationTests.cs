using System.Data;
using System.Text.RegularExpressions;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Web.Context;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Tests;

/// <summary>
/// A distributor with several locations is registered as one company per location (PT Primatrans is
/// split this way), so each company carries a free-text location that the Companies screens and the
/// Power BI views show beside its business unit, channel and salesman.
/// </summary>
public sealed class CompanyLocationTests
{
    [Fact]
    public void Model_stores_an_optional_location_of_at_most_100_characters()
    {
        using var db = new GiselXDbContext(new DbContextOptionsBuilder<GiselXDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=GiselXCompanyLocationTest;Trusted_Connection=True")
            .Options);
        var property = db.Model.FindEntityType(typeof(Company))!.FindProperty(nameof(Company.Location))!;

        Assert.Equal(100, property.GetMaxLength());
        Assert.True(property.IsNullable);
    }

    [Theory]
    [InlineData("Company_Insert")]
    [InlineData("Company_Update")]
    [InlineData("Company_SelectById")]
    [InlineData("Company_Select")]
    public void Every_company_procedure_carries_the_location(string procedure)
    {
        var sql = Read("GiselX.Web", "StoredProcedures", $"{procedure}.sql");

        Assert.Contains("company.Location", sql, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Company_Insert")]
    [InlineData("Company_Update")]
    public void Saving_procedures_accept_the_location(string procedure)
    {
        var sql = Read("GiselX.Web", "StoredProcedures", $"{procedure}.sql");

        Assert.Contains("@Location NVARCHAR(100) = NULL", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Company_list_can_filter_and_sort_by_location()
    {
        var sql = Read("GiselX.Web", "StoredProcedures", "Company_Select.sql");

        Assert.Equal(2, Regex.Matches(sql, @"\(@Location IS NULL OR company\.Location LIKE @Location\)").Count);
        Assert.Contains("@SortColumn IN ('location','Location') THEN company.Location END ASC", sql, StringComparison.Ordinal);
        Assert.Contains("@SortColumn IN ('location','Location') THEN company.Location END DESC", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void Migration_adds_the_column_and_installs_all_four_procedures_from_their_readable_source()
    {
        var migration = Normalize(File.ReadAllText(MigrationFile()));

        Assert.Contains("\"Location\"", migration, StringComparison.Ordinal);
        // The migration keeps each body verbatim in a raw string and doubles the quotes only when it
        // wraps the body in EXEC at run time, so the readable source must appear exactly.
        foreach (var procedure in new[] { "Company_Insert", "Company_Update", "Company_SelectById", "Company_Select" })
            Assert.Contains(Normalize(Read("GiselX.Web", "StoredProcedures", $"{procedure}.sql")),
                migration, StringComparison.Ordinal);
    }

    [Fact]
    public void Manual_script_is_transactional_rerunnable_and_records_history()
    {
        var sql = File.ReadAllText(ScriptFile());
        var migrationId = Path.GetFileNameWithoutExtension(MigrationFile());

        Assert.Contains("SET XACT_ABORT ON", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("BEGIN TRANSACTION", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("sp_getapplock", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("COL_LENGTH(N'dbo.Company', N'Location') IS NULL", sql, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE dbo.Company ADD Location nvarchar(100) NULL", sql, StringComparison.Ordinal);
        Assert.Contains("sp_executesql", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains($"N'{migrationId}'", sql, StringComparison.Ordinal);
        Assert.Contains("INSERT INTO dbo.__EFMigrationsHistory", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DELETE FROM", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Manual_script_installs_each_procedure_from_its_readable_source()
    {
        var script = Normalize(File.ReadAllText(ScriptFile()));

        foreach (var procedure in new[] { "Company_Insert", "Company_Update", "Company_SelectById", "Company_Select" })
            Assert.Contains(Normalize(Read("GiselX.Web", "StoredProcedures", $"{procedure}.sql")).Replace("'", "''"),
                script, StringComparison.Ordinal);
    }

    [Fact]
    public void Both_power_bi_views_expose_the_company_location()
    {
        var sql = Read("GiselX.Web", "DatabaseScripts", "Reporting_StockAndSellout.sql");

        Assert.Equal(2, Regex.Matches(sql, @"company\.Location\s+AS CompanyLocation").Count);
    }

    [Fact]
    public async Task Creating_a_company_trims_its_location_and_stores_blank_as_none()
    {
        var repository = new RecordingCompanyRepository();
        var service = new CompanyService(repository, new NoItemMappings(), new RecordingTransaction(), new UserClaimDto());

        await service.CreateCompanyAsync(new CompanyDto { Name = "PT Primatrans Medan", Location = "  Medan  " }, default);
        await service.CreateCompanyAsync(new CompanyDto { Name = "PT Primatrans Binjai", Location = "   " }, default);

        Assert.Equal("Medan", repository.Saved[0].Location);
        Assert.Null(repository.Saved[1].Location);
    }

    [Fact]
    public async Task Updating_a_company_trims_its_location()
    {
        var repository = new RecordingCompanyRepository();
        var service = new CompanyService(repository, new NoItemMappings(), new RecordingTransaction(), new UserClaimDto());

        await service.UpdateCompanyAsync(new CompanyDto { Id = 7, Name = "PT Primatrans Binjai", Location = " Binjai " }, default);

        Assert.Equal("Binjai", repository.Saved.Single().Location);
    }

    [Fact]
    public async Task A_location_longer_than_100_characters_is_refused()
    {
        var repository = new RecordingCompanyRepository();
        var transaction = new RecordingTransaction();
        var service = new CompanyService(repository, new NoItemMappings(), transaction, new UserClaimDto());

        var response = await service.CreateCompanyAsync(
            new CompanyDto { Name = "PT Primatrans", Location = new string('x', 101) }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Location must not exceed 100 characters.", response.Errors);
        Assert.Empty(repository.Saved);
        Assert.False(transaction.CommitCalled);
    }

    private static string MigrationFile() =>
        Directory.GetFiles(Path.Combine(Root(), "GiselX.Web", "Context", "Migrations"), "*_AddCompanyLocation.cs")
            .Single(path => !path.EndsWith(".Designer.cs", StringComparison.Ordinal));

    private static string ScriptFile() =>
        Directory.GetFiles(Path.Combine(Root(), "GiselX.Web", "DatabaseScripts"), "*_AddCompanyLocation.sql").Single();

    private static string Read(params string[] parts) => File.ReadAllText(Path.Combine(Root(), Path.Combine(parts)));

    private static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim();

    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "GiselX.sln")))
            directory = directory.Parent;

        return directory?.FullName ?? Directory.GetCurrentDirectory();
    }

    private sealed class RecordingCompanyRepository : ICompanyRepository
    {
        public List<Company> Saved { get; } = [];

        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken)
        { Saved.Add(company); return Task.FromResult(company); }
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<Company?>(new Company { Id = id, Name = "Existing" });
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedList<Company>([], 1, 10, 0));
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken)
        { Saved.Add(company); return Task.FromResult(company); }
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Company>>([]);
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Company>>([]);
    }

    private sealed class NoItemMappings : IItemMappingRepository
    {
        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>([]);
        public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>([]);
        public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<ItemMapping?>(null);
        public Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(ItemMappingDeleteResult.NotFound);
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId,
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<UnmappedDistributorItem>>([]);
    }

    private sealed class RecordingTransaction : IDbTransaction
    {
        public bool CommitCalled { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCalled = true;
        public void Rollback() { }
        public void Dispose() { }
    }
}
