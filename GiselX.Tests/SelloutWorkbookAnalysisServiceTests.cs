using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Tests;

public sealed class SelloutWorkbookAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_returns_ordered_weeks_and_derives_multiweek()
    {
        var mapping = Mapping();
        var repository = new FakeMappingRepository(mapping);
        var inspector = new FakeInspector { MatchFirstMapping = true };
        var service = new SelloutWorkbookAnalysisService(repository, inspector, new SelloutWorkbookParser(), new Resolver());
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data",
            CoreCells(3, "2026-07-04").Concat(CoreCells(4, "2026-07-06")).ToArray());

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.Equal("Weekly", result.Mapping!.Name);
        Assert.True(result.IsMultiWeek);
        Assert.Equal([new DateTime(2026, 6, 29), new DateTime(2026, 7, 6)],
            result.WeekSummaries.Select(week => week.ReportingWeekStart));
        Assert.Equal(1, repository.ReadCount);
        Assert.Equal(7, repository.LastCompanyId);
    }

    [Fact]
    public async Task AnalyzeAsync_propagates_mapping_detection_failure_without_parsing()
    {
        var repository = new FakeMappingRepository(Mapping());
        var inspector = new FakeInspector
        {
            DetectionErrors = ["No active distributor mapping exactly matches this workbook."]
        };
        var service = new SelloutWorkbookAnalysisService(repository, inspector, new SelloutWorkbookParser(), new Resolver());

        var result = await service.AnalyzeAsync([1, 2, 3], 7, CancellationToken.None);

        Assert.Null(result.Mapping);
        Assert.Empty(result.Rows);
        Assert.Contains("No active distributor mapping", Assert.Single(result.Errors));
    }

    [Fact]
    public async Task AnalyzeAsync_replaces_uploaded_sales_identity_with_gmk_identity_and_keeps_distributor_identity()
    {
        var mapping = Mapping();
        var service = new SelloutWorkbookAnalysisService(new FakeMappingRepository(mapping),
            new FakeInspector { MatchFirstMapping = true }, new SelloutWorkbookParser(), new Resolver(new ItemIdentityResolutionDto
            {
                DistributorItemId = "P3", DistributorItemName = "Uploaded Product", GmkItemId = "4100039",
                GmkItemName = "GMK Name", ItemMappingId = 9
            }));
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", CoreCells(3, "2026-07-04").ToArray());

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);
        var row = Assert.Single(result.Rows);

        Assert.True(row.IsValid);
        Assert.Equal("4100039", row.ProductId);
        Assert.Equal("GMK Name", row.ProductName);
        Assert.Equal("P3", row.DistributorProductId);
        Assert.Equal("Uploaded Product", row.DistributorProductName);
        Assert.Equal(9, row.ItemMappingId);
    }

    [Fact]
    public async Task AnalyzeAsync_rejects_sales_group_when_an_item_has_no_active_gmk_mapping()
    {
        var mapping = Mapping();
        var service = new SelloutWorkbookAnalysisService(new FakeMappingRepository(mapping),
            new FakeInspector { MatchFirstMapping = true }, new SelloutWorkbookParser(), new Resolver(new ItemIdentityResolutionDto
            {
                DistributorItemId = "P3", DistributorItemName = "Product",
                ErrorReason = "Distributor Item ID \"P3\" has no active GMK item mapping."
            }));
        var workbook = ClosedXmlSelloutWorkbookInspectorTests.BuildWorkbook("Data", CoreCells(3, "2026-07-04").ToArray());

        var result = await service.AnalyzeAsync(workbook, 7, CancellationToken.None);

        Assert.False(result.IsValid);
        var error = Assert.Single(result.RowErrors);
        Assert.Equal(SelloutCanonicalField.ProductId, error.Field);
        Assert.Contains("no active GMK item mapping", error.Reason);
    }

    private static DistributorTemplateMapping Mapping()
    {
        var fields = new[]
        {
            SelloutCanonicalField.CustomerId, SelloutCanonicalField.CustomerName,
            SelloutCanonicalField.ProductId, SelloutCanonicalField.ProductName,
            SelloutCanonicalField.InvoiceNo, SelloutCanonicalField.InvoiceDate,
            SelloutCanonicalField.InvoiceQty, SelloutCanonicalField.InvoiceUnit
        };
        return new DistributorTemplateMapping
        {
            Id = 11,
            CompanyId = 7,
            Name = "Weekly",
            WorksheetName = "Data",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 3,
            HeaderSignature = "unused-by-fake",
            IsActive = true,
            Columns = fields.Select((field, index) => new DistributorTemplateColumn
            {
                SourceColumnNumber = index + 1,
                SourceHeader = field.ToString(),
                CanonicalField = field
            }).ToList()
        };
    }

    private static IEnumerable<(int Row, int Column, string Value)> CoreCells(int row, string date) =>
    [
        (row, 1, $"C{row}"), (row, 2, "Customer"), (row, 3, $"P{row}"), (row, 4, "Product"),
        (row, 5, $"INV-{row}"), (row, 6, date), (row, 7, "1"), (row, 8, "PCS")
    ];

    private sealed class FakeMappingRepository(params DistributorTemplateMapping[] mappings)
        : IDistributorTemplateMappingRepository
    {
        public int ReadCount { get; private set; }
        public int LastCompanyId { get; private set; }

        public Task<IReadOnlyList<DistributorTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken)
        {
            ReadCount++;
            LastCompanyId = companyId;
            return Task.FromResult<IReadOnlyList<DistributorTemplateMapping>>(
                mappings.Where(mapping => mapping.CompanyId == companyId && mapping.IsActive).ToList());
        }

        public Task<IReadOnlyList<DistributorTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DistributorTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> InsertAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(DistributorTemplateMapping mapping, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<DistributorTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class FakeInspector : ISelloutWorkbookInspector
    {
        public bool MatchFirstMapping { get; init; }
        public List<string> DetectionErrors { get; init; } = [];

        public Task<SelloutMappingDetectionResultDto> DetectAsync(byte[] workbook,
            IReadOnlyCollection<DistributorTemplateMappingDto> mappings, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SelloutMappingDetectionResultDto
            {
                Mapping = MatchFirstMapping ? mappings.First() : null,
                Errors = DetectionErrors
            });

        public Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
            int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
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
