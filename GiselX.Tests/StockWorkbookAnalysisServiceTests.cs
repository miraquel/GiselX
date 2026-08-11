using GiselX.Domain;
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
        var service = new StockWorkbookAnalysisService(new Repository(mapping), new Inspector(mapping),
            new StockWorkbookParser());
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
        var service = new StockWorkbookAnalysisService(new Repository(Mapping()), new Inspector(null),
            new StockWorkbookParser());

        var result = await service.AnalyzeAsync([1], 7, CancellationToken.None);

        Assert.Null(result.Mapping);
        Assert.Empty(result.Rows);
        Assert.Contains("No active Stock mapping", Assert.Single(result.Errors));
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

    private sealed class Repository(StockTemplateMapping mapping) : IStockTemplateMappingRepository
    {
        public Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockTemplateMapping>>(mapping.CompanyId == companyId ? [mapping] : []);
        public Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<int> InsertAsync(StockTemplateMapping value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task UpdateAsync(StockTemplateMapping value, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Inspector(StockTemplateMapping? mapping) : IStockWorkbookInspector
    {
        public Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
            IReadOnlyCollection<StockTemplateMappingDto> mappings, CancellationToken cancellationToken = default) =>
            Task.FromResult(mapping is null
                ? new StockMappingDetectionResultDto { Errors = ["No active Stock mapping exactly matches this workbook."] }
                : new StockMappingDetectionResultDto { Mapping = mappings.Single() });
        public Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName, int headerRowNumber,
            int firstDataRowNumber, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
