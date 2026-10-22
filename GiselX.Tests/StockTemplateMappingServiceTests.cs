using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using Microsoft.AspNetCore.Http;

namespace GiselX.Tests;

public sealed class StockTemplateMappingServiceTests
{
    [Fact]
    public async Task SaveAsync_rejects_a_mapping_without_any_quantity_column()
    {
        var dto = ValidMapping();
        dto.Columns.RemoveAll(column => column.CanonicalField == StockCanonicalField.QtyPcs);
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("Map at least one stock quantity column: QtyCarton or QtyPcs.", response.Errors);
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_persists_valid_company_mapping_and_commits()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(ValidMapping(), "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Equal(2, mapping.CompanyId);
        Assert.Equal("admin", mapping.CreatedBy);
        Assert.Equal(3, mapping.Columns.Count);
        Assert.True(transaction.CommitCalled);
        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task SaveAsync_accepts_mapping_without_optional_price_unit()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(ValidMapping(), "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.DoesNotContain(Assert.Single(repository.Inserted).Columns,
            column => column.CanonicalField == StockCanonicalField.PriceUnit);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_allows_one_source_column_for_multiple_canonical_fields()
    {
        var dto = ValidMapping();
        dto.Columns[1].SourceColumnNumber = dto.Columns[0].SourceColumnNumber;
        dto.Columns[1].SourceHeader = dto.Columns[0].SourceHeader;
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Equal(2, mapping.Columns.Count(column =>
            column.SourceColumnNumber == dto.Columns[0].SourceColumnNumber));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_updates_requested_state_and_audit_then_commits()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new StockTemplateMapping { Id = 7, CompanyId = 2, Name = "Daily" });

        var response = await service.SetActiveAsync(7, 2, true, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var change = Assert.IsType<ActiveChange>(repository.ActiveChange);
        Assert.Equal((7, 2, true, "admin"),
            (change.Id, change.CompanyId, change.IsActive, change.ModifiedBy));
        Assert.InRange(change.ModifiedDate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_rejects_cross_company_mapping_without_commit()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new StockTemplateMapping { Id = 7, CompanyId = 2, Name = "Daily" });

        var response = await service.SetActiveAsync(7, 99, true, "admin", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(repository.ActiveChange);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task Mapping_condition_requires_both_value_lists()
    {
        var dto = ConditionMapping("GOOD", null);
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("Map at least one Good value and one Bad value when Condition is mapped.", response.Errors);
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task A_value_in_both_lists_is_rejected()
    {
        var dto = ConditionMapping("GS; BS", "bs;BAD");
        var (service, repository, _) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("These values are listed as both Good and Bad: BS.", response.Errors);
        Assert.Empty(repository.Inserted);
    }

    [Fact]
    public async Task Lists_are_ignored_and_cleared_when_condition_is_not_mapped()
    {
        var dto = ValidMapping();
        dto.GoodConditionValues = "GOOD";
        dto.BadConditionValues = "BAD";
        var (service, repository, _) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Null(mapping.GoodConditionValues);
        Assert.Null(mapping.BadConditionValues);
    }

    [Fact]
    public async Task Lists_are_stored_normalised()
    {
        var dto = ConditionMapping(" good ; GS;gs", "BAD;;  RUSAK ");
        var (service, repository, _) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var mapping = Assert.Single(repository.Inserted);
        Assert.Equal("good;GS", mapping.GoodConditionValues);
        Assert.Equal("BAD;RUSAK", mapping.BadConditionValues);
    }

    [Fact]
    public async Task Good_values_longer_than_500_characters_are_rejected()
    {
        var dto = ConditionMapping(LongList(), "BAD");
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("Good values must not exceed 500 characters in total.", response.Errors);
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task Bad_values_longer_than_500_characters_are_rejected()
    {
        var dto = ConditionMapping("GOOD", LongList());
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains("Bad values must not exceed 500 characters in total.", response.Errors);
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    private static string LongList() => string.Join(";", Enumerable.Range(0, 60).Select(i => $"VALUE{i:000}"));

    private static SaveStockTemplateMappingDto ConditionMapping(string? good, string? bad)
    {
        var dto = ValidMapping();
        dto.Columns.Add(Column(4, StockCanonicalField.Condition));
        dto.GoodConditionValues = good;
        dto.BadConditionValues = bad;
        return dto;
    }

    private static SaveStockTemplateMappingDto ValidMapping() => new()
    {
        CompanyId = 2,
        Name = "Stock Daily",
        WorksheetName = "Stock",
        HeaderRowNumber = 1,
        FirstDataRowNumber = 3,
        HeaderSignature = "PRODUCT ID\u001FPRODUCT NAME\u001FSALDO AKHIR",
        Columns =
        [
            Column(1, StockCanonicalField.ProductId),
            Column(2, StockCanonicalField.ProductName),
            Column(3, StockCanonicalField.QtyPcs)
        ]
    };

    private static StockTemplateColumnDto Column(int source, StockCanonicalField field) => new()
    {
        SourceColumnNumber = source,
        SourceHeader = field.ToString(),
        CanonicalField = field
    };

    private static (StockTemplateMappingService Service, FakeRepository Repository, FakeTransaction Transaction) CreateService()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        return (new StockTemplateMappingService(repository, transaction), repository, transaction);
    }

    private sealed class FakeRepository : IStockTemplateMappingRepository
    {
        public List<StockTemplateMapping> Mappings { get; } = [];
        public List<StockTemplateMapping> Inserted { get; } = [];
        public ActiveChange? ActiveChange { get; private set; }

        public Task<IReadOnlyList<StockTemplateMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<StockTemplateMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockTemplateMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId && mapping.IsActive).ToList());
        public Task<StockTemplateMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Mappings.SingleOrDefault(mapping => mapping.Id == id && mapping.CompanyId == companyId));
        public Task<int> InsertAsync(StockTemplateMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(42);
        }
        public Task UpdateAsync(StockTemplateMapping mapping, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken)
        {
            ActiveChange = new ActiveChange(id, companyId, isActive, modifiedBy, modifiedDate);
            return Task.CompletedTask;
        }
        public Task<StockTemplateMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(StockTemplateMappingDeleteResult.Deleted);
    }

    private sealed record ActiveChange(int Id, int CompanyId, bool IsActive, string ModifiedBy,
        DateTime ModifiedDate);

    private sealed class FakeTransaction : IDbTransaction
    {
        public bool CommitCalled { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCalled = true;
        public void Dispose() { }
        public void Rollback() { }
    }
}
