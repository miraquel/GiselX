using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class MappingCopyServiceTests
{
    private const int Source = 1;
    private const int Target = 2;

    [Fact]
    public async Task PreviewAsync_refuses_copying_a_company_onto_itself()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.PreviewAsync(Source, Source, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
    }

    [Fact]
    public async Task PreviewAsync_refuses_a_company_that_does_not_exist()
    {
        var fixture = new Fixture();

        var response = await fixture.Service.PreviewAsync(Source, 99, CancellationToken.None);

        Assert.Equal(404, response.StatusCode);
    }

    [Fact]
    public async Task PreviewAsync_marks_a_template_whose_name_exists_on_the_target_as_skipped()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(10, Source, "Weekly", "SIG-A"));
        fixture.Sellout.Mappings.Add(SelloutTemplate(20, Target, " weekly ", "SIG-B"));

        var preview = (await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None)).Data!;

        Assert.Equal(TemplateCopyOutcome.SkippedNameExists, Assert.Single(preview.SelloutTemplates).Outcome);
    }

    [Fact]
    public async Task PreviewAsync_marks_an_active_sellout_template_with_a_signature_already_active_on_the_target_as_skipped()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(10, Source, "Weekly", "SIG-A"));
        fixture.Sellout.Mappings.Add(SelloutTemplate(20, Target, "Medan format", "SIG-A"));

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.SelloutTemplates);

        Assert.Equal(TemplateCopyOutcome.SkippedFormatExists, candidate.Outcome);
        Assert.Contains("This company already has an active template for this format.", candidate.Reasons);
    }

    [Fact]
    public async Task PreviewAsync_does_not_skip_an_inactive_template_for_a_competing_format()
    {
        var fixture = new Fixture();
        var inactive = SelloutTemplate(10, Source, "Old weekly", "SIG-A");
        inactive.IsActive = false;
        fixture.Sellout.Mappings.Add(inactive);
        fixture.Sellout.Mappings.Add(SelloutTemplate(20, Target, "Medan format", "SIG-A"));

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.SelloutTemplates);

        Assert.Equal(TemplateCopyOutcome.WillCopy, candidate.Outcome);
        Assert.False(candidate.IsActive);
    }

    [Fact]
    public async Task PreviewAsync_compares_stock_formats_per_worksheet()
    {
        var fixture = new Fixture();
        fixture.Stock.Mappings.Add(StockTemplate(10, Source, "Stock Jakarta", "Sheet A", "SIG-S"));
        fixture.Stock.Mappings.Add(StockTemplate(20, Target, "Stock Medan", "Sheet B", "SIG-S"));

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.StockTemplates);

        Assert.Equal(TemplateCopyOutcome.WillCopy, candidate.Outcome);
    }

    [Fact]
    public async Task PreviewAsync_skips_a_stock_template_for_the_same_worksheet_ignoring_case()
    {
        var fixture = new Fixture();
        fixture.Stock.Mappings.Add(StockTemplate(10, Source, "Stock Jakarta", "Sheet A", "SIG-S"));
        fixture.Stock.Mappings.Add(StockTemplate(20, Target, "Stock Medan", "SHEET a", "SIG-S"));

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.StockTemplates);

        Assert.Equal(TemplateCopyOutcome.SkippedFormatExists, candidate.Outcome);
    }

    [Fact]
    public async Task PreviewAsync_refuses_a_stock_template_without_area_when_the_target_has_areas()
    {
        var fixture = new Fixture();
        fixture.Areas[Target] = [new StockArea { Id = 1, CompanyId = Target, Name = "Gudang Utama" }];
        fixture.Stock.Mappings.Add(StockTemplate(10, Source, "Stock Jakarta", "Sheet A", "SIG-S"));

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.StockTemplates);

        Assert.Equal(TemplateCopyOutcome.Invalid, candidate.Outcome);
        Assert.Contains(candidate.Reasons, reason => reason.Contains("must map an Area column", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviewAsync_reports_a_legacy_template_missing_a_required_field_as_invalid()
    {
        var fixture = new Fixture();
        var legacy = SelloutTemplate(10, Source, "Legacy", "SIG-A");
        legacy.Columns.Remove(legacy.Columns.First(column => column.CanonicalField == SelloutCanonicalField.CustomerName));
        fixture.Sellout.Mappings.Add(legacy);

        var candidate = Assert.Single((await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None))
            .Data!.SelloutTemplates);

        Assert.Equal(TemplateCopyOutcome.Invalid, candidate.Outcome);
        Assert.Contains(candidate.Reasons, reason => reason.StartsWith("Required canonical field", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PreviewAsync_counts_item_mappings_to_copy_and_skip_case_insensitively()
    {
        var fixture = new Fixture();
        fixture.Items.Mappings.Add(Item(Source, " d-1 "));
        fixture.Items.Mappings.Add(Item(Source, "D-2"));
        fixture.Items.Mappings.Add(Item(Target, "D-1"));

        var preview = (await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None)).Data!;

        Assert.Equal(1, preview.ItemMappingsToCopy);
        Assert.Equal(1, preview.ItemMappingsSkipped);
    }

    [Fact]
    public async Task PreviewAsync_reports_whether_the_target_has_item_mapping_enabled()
    {
        var fixture = new Fixture();
        fixture.Companies[Target].ItemMappingEnabled = false;

        var preview = (await fixture.Service.PreviewAsync(Source, Target, CancellationToken.None)).Data!;

        Assert.False(preview.TargetItemMappingEnabled);
    }

    [Fact]
    public async Task CopyAsync_inserts_selected_templates_for_the_target_and_commits_once()
    {
        var fixture = new Fixture();
        var sellout = SelloutTemplate(10, Source, "Weekly", "SIG-A");
        var stock = StockTemplate(11, Source, "Stock Jakarta", "Sheet A", "SIG-S");
        stock.GoodConditionValues = "BAIK";
        stock.BadConditionValues = "RUSAK";
        stock.IsActive = false;
        fixture.Sellout.Mappings.Add(sellout);
        fixture.Stock.Mappings.Add(stock);

        var response = await fixture.Service.CopyAsync(Request(sellout: [10], stock: [11]), "admin",
            CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal(1, response.Data!.SelloutCopied);
        Assert.Equal(1, response.Data.StockCopied);
        var copiedSellout = Assert.Single(fixture.Sellout.Inserted);
        Assert.Equal(Target, copiedSellout.CompanyId);
        Assert.Equal("admin", copiedSellout.CreatedBy);
        Assert.Equal("Weekly", copiedSellout.Name);
        Assert.Equal("SIG-A", copiedSellout.HeaderSignature);
        Assert.Equal(sellout.Columns.Select(column => (column.SourceColumnNumber, column.SourceHeader, column.CanonicalField)),
            copiedSellout.Columns.Select(column => (column.SourceColumnNumber, column.SourceHeader, column.CanonicalField)));
        var copiedStock = Assert.Single(fixture.Stock.Inserted);
        Assert.Equal(Target, copiedStock.CompanyId);
        Assert.False(copiedStock.IsActive);
        Assert.Equal("BAIK", copiedStock.GoodConditionValues);
        Assert.Equal("RUSAK", copiedStock.BadConditionValues);
        Assert.Equal(stock.Columns.Count, copiedStock.Columns.Count);
        Assert.Equal(1, fixture.Transaction.CommitCount);
    }

    [Fact]
    public async Task CopyAsync_does_not_let_two_selected_active_copies_of_one_format_both_land()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(10, Source, "Weekly A", "SIG-A"));
        fixture.Sellout.Mappings.Add(SelloutTemplate(11, Source, "Weekly B", "SIG-A"));

        var response = await fixture.Service.CopyAsync(Request(sellout: [10, 11]), "admin", CancellationToken.None);

        Assert.Single(fixture.Sellout.Inserted);
        Assert.Single(response.Data!.Skipped);
    }

    [Fact]
    public async Task CopyAsync_copies_item_mappings_with_their_source_status_and_skips_existing_ones()
    {
        var fixture = new Fixture();
        var inactive = Item(Source, "D-2");
        inactive.IsActive = false;
        fixture.Items.Mappings.Add(Item(Source, "D-1"));
        fixture.Items.Mappings.Add(inactive);
        fixture.Items.Mappings.Add(Item(Target, "d-1"));

        var response = await fixture.Service.CopyAsync(Request(items: true), "admin", CancellationToken.None);

        var copied = Assert.Single(fixture.Items.Inserted);
        Assert.Equal(Target, copied.CompanyId);
        Assert.Equal("D-2", copied.DistributorItemId);
        Assert.Equal("GMK-D-2", copied.GmkItemId);
        Assert.False(copied.IsActive);
        Assert.Equal("admin", copied.CreatedBy);
        Assert.Equal(1, response.Data!.ItemMappingsCopied);
        Assert.Equal(1, response.Data.ItemMappingsSkipped);
        Assert.Equal(1, fixture.Transaction.CommitCount);
    }

    [Fact]
    public async Task CopyAsync_ignores_ids_that_do_not_belong_to_the_source_company()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(30, 3, "Other company", "SIG-X"));

        await fixture.Service.CopyAsync(Request(sellout: [30]), "admin", CancellationToken.None);

        Assert.Empty(fixture.Sellout.Inserted);
    }

    [Fact]
    public async Task CopyAsync_with_nothing_copyable_does_not_commit()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(10, Source, "Weekly", "SIG-A"));
        fixture.Sellout.Mappings.Add(SelloutTemplate(20, Target, "Weekly", "SIG-B"));

        var response = await fixture.Service.CopyAsync(Request(sellout: [10]), "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal("Nothing was copied.", response.Message);
        Assert.Equal(0, fixture.Transaction.CommitCount);
    }

    [Fact]
    public async Task CopyAsync_refuses_copying_a_company_onto_itself_without_writing()
    {
        var fixture = new Fixture();
        fixture.Sellout.Mappings.Add(SelloutTemplate(10, Source, "Weekly", "SIG-A"));

        var response = await fixture.Service.CopyAsync(new MappingCopyRequestDto
        {
            SourceCompanyId = Source, TargetCompanyId = Source, SelloutMappingIds = [10]
        }, "admin", CancellationToken.None);

        Assert.Equal(400, response.StatusCode);
        Assert.Empty(fixture.Sellout.Inserted);
        Assert.Equal(0, fixture.Transaction.CommitCount);
    }

    [Fact]
    public async Task ListSourceCompaniesAsync_excludes_the_target_and_appends_location()
    {
        var fixture = new Fixture();

        var options = await fixture.Service.ListSourceCompaniesAsync(Target, CancellationToken.None);

        Assert.DoesNotContain(options, option => option.Id == Target);
        Assert.Contains(options, option => option.Id == Source && option.DisplayName == "PT ABC – Jakarta");
        Assert.Contains(options, option => option.Id == 3 && option.DisplayName == "PT XYZ");
    }

    [Fact]
    public async Task GetCompanyAsync_returns_the_display_name_or_null_when_missing()
    {
        var fixture = new Fixture();

        Assert.Equal("PT ABC – Medan", (await fixture.Service.GetCompanyAsync(Target, CancellationToken.None))!.DisplayName);
        Assert.Null(await fixture.Service.GetCompanyAsync(99, CancellationToken.None));
    }

    private static MappingCopyRequestDto Request(List<int>? sellout = null, List<int>? stock = null,
        bool items = false) => new()
    {
        SourceCompanyId = Source,
        TargetCompanyId = Target,
        SelloutMappingIds = sellout ?? [],
        StockMappingIds = stock ?? [],
        CopyItemMappings = items
    };

    private static DistributorTemplateMapping SelloutTemplate(int id, int companyId, string name, string signature) => new()
    {
        Id = id,
        CompanyId = companyId,
        Name = name,
        WorksheetName = "Sheet1",
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = signature,
        IsActive = true,
        Columns = SelloutCanonicalFieldCatalog.RequiredFields.OrderBy(field => field)
            .Select((field, index) => new DistributorTemplateColumn
            {
                SourceColumnNumber = index + 1,
                SourceHeader = field.ToString(),
                CanonicalField = field
            }).ToList()
    };

    private static StockTemplateMapping StockTemplate(int id, int companyId, string name, string worksheet,
        string signature) => new()
    {
        Id = id,
        CompanyId = companyId,
        Name = name,
        WorksheetName = worksheet,
        HeaderRowNumber = 1,
        FirstDataRowNumber = 2,
        HeaderSignature = signature,
        IsActive = true,
        Columns =
        [
            new StockTemplateColumn { SourceColumnNumber = 1, SourceHeader = "KODE", CanonicalField = StockCanonicalField.ProductId },
            new StockTemplateColumn { SourceColumnNumber = 2, SourceHeader = "NAMA", CanonicalField = StockCanonicalField.ProductName },
            new StockTemplateColumn { SourceColumnNumber = 3, SourceHeader = "PCS", CanonicalField = StockCanonicalField.QtyPcs }
        ]
    };

    private static ItemMapping Item(int companyId, string distributorItemId) => new()
    {
        CompanyId = companyId,
        DistributorItemId = distributorItemId,
        DistributorItemName = "Item " + distributorItemId.Trim(),
        GmkItemId = "GMK-" + distributorItemId.Trim(),
        GmkItemName = "GMK " + distributorItemId.Trim(),
        IsActive = true,
        CreatedBy = "seed"
    };

    private sealed class Fixture
    {
        public Dictionary<int, Company> Companies { get; } = new()
        {
            [Source] = new Company { Id = Source, Name = "PT ABC", Location = "Jakarta", ItemMappingEnabled = true },
            [Target] = new Company { Id = Target, Name = "PT ABC", Location = "Medan", ItemMappingEnabled = true },
            [3] = new Company { Id = 3, Name = "PT XYZ", Location = " " }
        };
        public Dictionary<int, IReadOnlyList<StockArea>> Areas { get; } = [];
        public FakeSelloutRepository Sellout { get; } = new();
        public FakeStockRepository Stock { get; } = new();
        public FakeItemRepository Items { get; } = new();
        public CountingTransaction Transaction { get; } = new();

        public MappingCopyService Service => new(new FakeCompanyRepository(Companies), Sellout, Stock, Items,
            new FakeAreaRepository(Areas), Transaction);
    }

    private sealed class FakeCompanyRepository(Dictionary<int, Company> companies) : ICompanyRepository
    {
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(companies.GetValueOrDefault(id));
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Company>>(companies.Values.ToList());
        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeSelloutRepository : IDistributorTemplateMappingRepository
    {
        public List<DistributorTemplateMapping> Mappings { get; } = [];
        public List<DistributorTemplateMapping> Inserted { get; } = [];

        public Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DistributorTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<DistributorTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(100 + Inserted.Count);
        }
        public Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DistributorTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeStockRepository : IStockTemplateMappingRepository
    {
        public List<StockTemplateMapping> Mappings { get; } = [];
        public List<StockTemplateMapping> Inserted { get; } = [];

        public Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> InsertAsync(StockTemplateMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(200 + Inserted.Count);
        }
        public Task UpdateAsync(StockTemplateMapping mapping, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeItemRepository : IItemMappingRepository
    {
        public List<ItemMapping> Mappings { get; } = [];
        public List<ItemMapping> Inserted { get; } = [];

        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(300 + Inserted.Count);
        }
        public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<GmkItemSuggestion>> GetGmkItemSuggestionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GmkItemSuggestion>>([]);
    }

    private sealed class FakeAreaRepository(Dictionary<int, IReadOnlyList<StockArea>> areas) : IStockAreaRepository
    {
        public Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult(areas.GetValueOrDefault(companyId) ?? []);
        public Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(StockArea area, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class CountingTransaction : System.Data.IDbTransaction
    {
        public int CommitCount { get; private set; }
        public System.Data.IDbConnection? Connection => null;
        public System.Data.IsolationLevel IsolationLevel => System.Data.IsolationLevel.ReadCommitted;
        public void Commit() => CommitCount++;
        public void Rollback() { }
        public void Dispose() { }
    }
}
