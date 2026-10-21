using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Tests;

public sealed class StockWorkbookAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_returns_valid_and_invalid_rows_from_company_mapping()
    {
        var mapping = Mapping();
        var service = new StockWorkbookAnalysisService(new Repository(mapping), new Inspector(),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver());
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "Valid"), (3, 3, "4"), (4, 1, "P2"), (4, 2, "Invalid"), (4, 3, "-1")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal("Daily Stock", result.Mapping!.Name);
        Assert.Equal(2, result.Rows.Count);
        Assert.Equal(1, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        Assert.Contains(result.RowErrors, error => error.Field == StockCanonicalField.SaldoAkhir);
    }

    [Fact]
    public async Task AnalyzeAsync_propagates_detection_failure_without_parsing()
    {
        var service = new StockWorkbookAnalysisService(new Repository(Mapping()), new Inspector(matches: false),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver());

        var result = await service.AnalyzeAsync([1], 7, CancellationToken.None);

        Assert.Null(result.Mapping);
        Assert.Empty(result.Rows);
        Assert.Contains("No active Stock mapping", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task AnalyzeAsync_replaces_uploaded_stock_identity_with_gmk_identity_and_keeps_distributor_identity()
    {
        var mapping = Mapping();
        var service = new StockWorkbookAnalysisService(new Repository(mapping), new Inspector(),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver(new ItemIdentityResolutionDto
            {
                DistributorItemId = "P1", DistributorItemName = "Uploaded Name", GmkItemId = "4100039",
                GmkItemName = "GMK Name", ItemMappingId = 9
            }));
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "Uploaded Name"), (3, 3, "4")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);
        var row = Assert.Single(result.Rows);

        Assert.True(row.IsValid);
        Assert.Equal("4100039", row.ProductId);
        Assert.Equal("GMK Name", row.ProductName);
        Assert.Equal("P1", row.DistributorProductId);
        Assert.Equal("Uploaded Name", row.DistributorProductName);
        Assert.Equal(9, row.ItemMappingId);
    }

    [Fact]
    public async Task AnalyzeAsync_marks_unmapped_stock_row_invalid_with_a_product_id_error()
    {
        var mapping = Mapping();
        var service = new StockWorkbookAnalysisService(new Repository(mapping), new Inspector(),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver(new ItemIdentityResolutionDto
            {
                DistributorItemId = "P1", DistributorItemName = "Name",
                ErrorReason = "Distributor Item ID \"P1\" has no active GMK item mapping."
            }));
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "Name"), (3, 3, "4")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal(0, result.ValidRows);
        Assert.Equal(1, result.InvalidRows);
        var error = Assert.Single(result.RowErrors);
        Assert.Equal(StockCanonicalField.ProductId, error.Field);
        Assert.Contains("no active GMK item mapping", error.Reason);
    }

    [Fact]
    public async Task AnalyzeAsync_combines_rows_from_every_mapped_worksheet()
    {
        var first = Mapping();
        var second = Mapping();
        second.Id = 12;
        second.Name = "Weekly Stock";
        second.WorksheetName = "Stock 2";
        var service = new StockWorkbookAnalysisService(new Repository(first, second), new Inspector(),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver());
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Stock", [(3, 1, "P1"), (3, 2, "Sheet one"), (3, 3, "4")]),
            ("Stock 2", [(3, 1, "P2"), (3, 2, "Sheet two"), (3, 3, "9"), (4, 1, "P3"), (4, 2, "Sheet two"), (4, 3, "1")]));

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal(3, result.Rows.Count);
        Assert.Equal(3, result.ValidRows);
        Assert.Null(result.Mapping);
        Assert.Equal(["Stock", "Stock 2"], result.Sheets.Select(sheet => sheet.WorksheetName));
        Assert.Equal([1, 2], result.Sheets.Select(sheet => sheet.TotalRows));
        Assert.Equal(["Daily Stock", "Weekly Stock"], result.Sheets.Select(sheet => sheet.MappingName));
        Assert.Equal([11, 12, 12], result.Rows.Select(row => row.StockTemplateMappingId));
        Assert.Equal(["Stock", "Stock 2", "Stock 2"], result.Rows.Select(row => row.SourceWorksheetName));
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task AnalyzeAsync_keeps_the_single_mapping_and_reports_skipped_worksheets()
    {
        var service = new StockWorkbookAnalysisService(new Repository(Mapping()),
            new Inspector(true, "Cover"), new StockWorkbookParser(), NoAreas(), Companies(), new Resolver());
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Cover", [(1, 1, "Laporan stok")]),
            ("Stock", [(3, 1, "P1"), (3, 2, "Only sheet"), (3, 3, "4")]));

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal("Daily Stock", result.Mapping!.Name);
        Assert.Equal(["Cover"], result.IgnoredWorksheetNames);
        var row = Assert.Single(result.Rows);
        Assert.Equal(11, row.StockTemplateMappingId);
        Assert.Equal("Stock", row.SourceWorksheetName);
    }

    [Fact]
    public async Task AnalyzeAsync_tags_row_errors_with_the_worksheet_they_came_from()
    {
        var first = Mapping();
        var second = Mapping();
        second.Id = 12;
        second.Name = "Weekly Stock";
        second.WorksheetName = "Stock 2";
        var service = new StockWorkbookAnalysisService(new Repository(first, second), new Inspector(),
            new StockWorkbookParser(), NoAreas(), Companies(), new Resolver());
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildMultiSheetWorkbook(
            ("Stock", [(3, 1, "P1"), (3, 2, "Valid"), (3, 3, "4")]),
            ("Stock 2", [(3, 1, "P2"), (3, 2, "Negative"), (3, 3, "-1")]));

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        var error = Assert.Single(result.RowErrors);
        Assert.Equal("Stock 2", error.WorksheetName);
        Assert.Equal(StockCanonicalField.SaldoAkhir, error.Field);
        Assert.Equal(1, result.Sheets.Single(sheet => sheet.WorksheetName == "Stock 2").InvalidRows);
        Assert.Equal(0, result.Sheets.Single(sheet => sheet.WorksheetName == "Stock").InvalidRows);
    }

    [Fact]
    public async Task Rows_get_the_area_and_condition_their_cells_resolve_to()
    {
        var service = AreaService(AreaAndConditionMapping(), [new StockArea { Id = 5, CompanyId = 7, Name = "Medan" }]);
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "N"), (3, 3, "4"), (3, 4, "medan"), (3, 5, "BAD"),
             (4, 1, "P2"), (4, 2, "N"), (4, 3, "4"), (4, 4, "Medan"), (4, 5, "good")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal(2, result.ValidRows);
        Assert.All(result.Rows, row => Assert.Equal(5, row.StockAreaId));
        Assert.Equal([StockCondition.Bad, StockCondition.Good], result.Rows.Select(row => row.Condition));
    }

    [Fact]
    public async Task An_unresolvable_area_invalidates_only_that_row_and_names_the_text()
    {
        var service = AreaService(AreaAndConditionMapping(), [new StockArea { Id = 5, CompanyId = 7, Name = "Medan" }]);
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "N"), (3, 3, "4"), (3, 4, "Narnia"), (3, 5, "GOOD"),
             (4, 1, "P2"), (4, 2, "N"), (4, 3, "4"), (4, 4, "Medan"), (4, 5, "GOOD")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal(1, result.ValidRows);
        var error = Assert.Single(result.RowErrors);
        Assert.Equal(StockCanonicalField.Area, error.Field);
        Assert.Equal("Narnia", error.RejectedValue);
        Assert.Equal("Stock", error.WorksheetName);
        Assert.Contains("'Narnia'", error.Reason);
        Assert.Contains("Acme", error.Reason);
        Assert.Contains("Narnia", result.Rows[0].ValidationErrorsJson);
        Assert.False(result.Rows[0].IsValid);
        Assert.True(result.Rows[1].IsValid);
    }

    [Fact]
    public async Task A_template_without_area_or_condition_yields_no_area_and_good()
    {
        var service = AreaService(Mapping(), []);
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "N"), (3, 3, "4")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        var row = Assert.Single(result.Rows);
        Assert.True(row.IsValid);
        Assert.Null(row.StockAreaId);
        Assert.Equal(StockCondition.Good, row.Condition);
    }

    [Fact]
    public async Task A_company_with_areas_refuses_a_template_that_does_not_map_area()
    {
        var service = AreaService(Mapping(), [new StockArea { Id = 5, CompanyId = 7, Name = "Medan" }]);
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "N"), (3, 3, "4")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal("Template 'Daily Stock' does not map an Area column, but Acme has Stock areas registered.",
            Assert.Single(result.Errors));
        Assert.Empty(result.Rows);
    }

    [Fact]
    public async Task Inactive_areas_do_not_resolve()
    {
        var service = AreaService(AreaAndConditionMapping(),
            [new StockArea { Id = 5, CompanyId = 7, Name = "Medan", IsActive = false },
             new StockArea { Id = 6, CompanyId = 7, Name = "Binjai" }]);
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Stock",
            [(3, 1, "P1"), (3, 2, "N"), (3, 3, "4"), (3, 4, "Medan"), (3, 5, "GOOD")]);

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal(0, result.ValidRows);
        Assert.Contains("'Medan'", Assert.Single(result.RowErrors).Reason);
    }

    private static StockWorkbookAnalysisService AreaService(StockTemplateMapping mapping, StockArea[] areas) =>
        new(new Repository(mapping), new Inspector(), new StockWorkbookParser(), new AreaLookup(areas), Companies(),
            new Resolver());

    private static StockTemplateMapping AreaAndConditionMapping()
    {
        var mapping = Mapping();
        mapping.GoodConditionValues = "GOOD";
        mapping.BadConditionValues = "BAD";
        mapping.Columns.Add(new() { SourceColumnNumber = 4, SourceHeader = "Area", CanonicalField = StockCanonicalField.Area });
        mapping.Columns.Add(new() { SourceColumnNumber = 5, SourceHeader = "Cond", CanonicalField = StockCanonicalField.Condition });
        return mapping;
    }

    private static AreaLookup NoAreas() => new([]);
    private static CompanyLookup Companies() => new();

    private sealed class AreaLookup(params StockArea[] areas) : IStockAreaRepository
    {
        public Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockArea>>(
                areas.Where(a => a.CompanyId == companyId).Where(a => !activeOnly || a.IsActive).ToList());
        public Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(StockArea area, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class CompanyLookup : ICompanyRepository
    {
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult<Company?>(new Company { Id = id, Name = "Acme" });
        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static StockTemplateMapping Mapping() => new()
    {
        Id = 11, CompanyId = 7, Name = "Daily Stock", WorksheetName = "Stock", HeaderRowNumber = 1,
        FirstDataRowNumber = 3, HeaderSignature = "signature", IsActive = true,
        Columns =
        [
            new() { SourceColumnNumber = 1, SourceHeader = "Product", CanonicalField = StockCanonicalField.ProductId },
            new() { SourceColumnNumber = 2, SourceHeader = "Name", CanonicalField = StockCanonicalField.ProductName },
            new() { SourceColumnNumber = 3, SourceHeader = "Saldo", CanonicalField = StockCanonicalField.SaldoAkhir }
        ]
    };

    private sealed class Repository(params StockTemplateMapping[] mappings) : IStockTemplateMappingRepository
    {
        public Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockTemplateMapping>>(
                mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> InsertAsync(StockTemplateMapping value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(StockTemplateMapping value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Inspector(bool matches = true, params string[] ignoredWorksheetNames)
        : IStockWorkbookInspector
    {
        public Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
            IReadOnlyCollection<StockTemplateMappingDto> mappings, CancellationToken cancellationToken = default) =>
            Task.FromResult(matches
                ? new StockMappingDetectionResultDto
                {
                    Matches = mappings.Select(mapping => new StockSheetMatchDto
                    {
                        Mapping = mapping,
                        WorksheetName = mapping.WorksheetName
                    }).ToList(),
                    IgnoredWorksheetNames = ignoredWorksheetNames.ToList()
                }
                : new StockMappingDetectionResultDto { Errors = ["No active Stock mapping exactly matches this workbook."] });
        public Task<WorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName, int headerRowNumber,
            int firstDataRowNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class Resolver(ItemIdentityResolutionDto? result = null) : IItemMappingResolver
    {
        public Task<ItemMappingContextDto> LoadAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new ItemMappingContextDto());
        public ItemIdentityResolutionDto Resolve(ItemMappingContextDto context, string? distributorItemId,
            string? distributorItemName) => result ?? new ItemIdentityResolutionDto
            {
                DistributorItemId = distributorItemId,
                DistributorItemName = distributorItemName,
                GmkItemId = distributorItemId,
                GmkItemName = distributorItemName
            };
    }
}
