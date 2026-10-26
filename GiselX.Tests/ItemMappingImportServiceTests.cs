using System.Data;
using ClosedXML.Excel;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Interface;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Tests;

public sealed class ItemMappingImportServiceTests
{
    [Fact]
    public void AddGiselXService_resolves_item_mapping_import_service()
    {
        var services = new ServiceCollection();
        services.AddGiselXService();
        services.AddScoped<IItemMappingRepository>(_ => new FakeRepository());
        services.AddScoped<IDbTransaction, FakeTransaction>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var service = scope.ServiceProvider.GetRequiredService<IItemMappingImportService>();

        Assert.IsType<ItemMappingImportService>(service);
    }

    [Fact]
    public async Task PreflightAsync_classifies_new_updated_unchanged_and_invalid_rows_without_writing()
    {
        var repository = new FakeRepository(
            Mapping(8, "P0003", "4100039", "Cream Caramel", "Caramel Mix", true),
            Mapping(9, "P0004", "4100040", "Old", "Old name", false));
        var transaction = new FakeTransaction();
        var service = new ItemMappingImportService(new ItemMappingWorkbookParser(), repository, transaction);

        var response = await service.PreflightAsync(Workbook(
            ("4100039", "Cream Caramel", "P0003", "Caramel Mix", "1"),
            ("4100041", "New", "P0004", "New name", "0"),
            ("4100042", "Another", "P0005", "Another name", "1"),
            ("", "Missing", "P0006", "Missing gmk", "1")), 7, default);

        Assert.False(response.IsSuccess);
        Assert.Equal((1, 1, 1, 1), (response.Data!.NewRows, response.Data.UpdatedRows,
            response.Data.UnchangedRows, response.Data.InvalidRows));
        Assert.Empty(repository.Upserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task ImportAsync_requires_confirmation_without_upserting_or_committing()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.ImportAsync(Workbook(("4100039", "Cream Caramel", "P0003", "Caramel Mix", "1")),
            7, false, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("confirm", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Upserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task ImportAsync_rejects_any_invalid_row_without_upserting_or_committing()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.ImportAsync(Workbook(
            ("4100039", "Cream Caramel", "P0003", "Caramel Mix", "1"),
            ("4100040", "Other", " p0003 ", "Other name", "0")), 7, true, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
        Assert.Equal(2, response.Data!.InvalidRows);
        Assert.Empty(repository.Upserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task ImportAsync_updates_existing_and_new_mappings_with_selected_status()
    {
        var existing = Mapping(8, "P0003", "OLD", "Old", "Old name", false);
        existing.CreatedBy = "original";
        existing.CreatedDate = new DateTime(2026, 1, 1);
        var repository = new FakeRepository(existing);
        var transaction = new FakeTransaction();
        var service = new ItemMappingImportService(new ItemMappingWorkbookParser(), repository, transaction);

        var response = await service.ImportAsync(Workbook(
            ("4100039", "Cream Caramel", "P0003", "Caramel Mix", "1"),
            ("4100040", "Other", "P0004", "Other name", "0")), 7, true, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal((1, 1, 0), (response.Data!.NewRows, response.Data.UpdatedRows, response.Data.UnchangedRows));
        var updated = Assert.Single(repository.Upserted, mapping => mapping.DistributorItemId == "P0003");
        Assert.Equal(8, updated.Id);
        Assert.True(updated.IsActive);
        Assert.Equal("original", updated.CreatedBy);
        Assert.Equal(new DateTime(2026, 1, 1), updated.CreatedDate);
        Assert.Equal("admin", updated.ModifiedBy);
        Assert.False(Assert.Single(repository.Upserted, mapping => mapping.DistributorItemId == "P0004").IsActive);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task ImportAsync_treats_case_only_distributor_item_id_change_as_unchanged()
    {
        var repository = new FakeRepository(
            Mapping(8, "P0003", "4100039", "Cream Caramel", "Caramel Mix", true));
        var transaction = new FakeTransaction();
        var service = new ItemMappingImportService(new ItemMappingWorkbookParser(), repository, transaction);

        var response = await service.ImportAsync(Workbook(
            ("4100039", "Cream Caramel", "p0003", "Caramel Mix", "1")), 7, true, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal(1, response.Data!.UnchangedRows);
        Assert.Equal(0, response.Data.UpdatedRows);
        Assert.Empty(repository.Upserted);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task ImportAsync_treats_status_only_change_as_an_update()
    {
        var repository = new FakeRepository(
            Mapping(8, "P0003", "4100039", "Cream Caramel", "Caramel Mix", true));
        var transaction = new FakeTransaction();
        var service = new ItemMappingImportService(new ItemMappingWorkbookParser(), repository, transaction);

        var response = await service.ImportAsync(Workbook(
            ("4100039", "Cream Caramel", "P0003", "Caramel Mix", "0")), 7, true, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal(1, response.Data!.UpdatedRows);
        Assert.Equal(0, response.Data.UnchangedRows);
        Assert.False(Assert.Single(repository.Upserted).IsActive);
        Assert.True(transaction.CommitCalled);
    }

    private static (ItemMappingImportService Service, FakeRepository Repository, FakeTransaction Transaction) CreateService()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        return (new ItemMappingImportService(new ItemMappingWorkbookParser(), repository, transaction), repository, transaction);
    }

    private static ItemMapping Mapping(int id, string distributorItemId, string gmkItemId, string gmkItemName,
        string distributorItemName, bool active) => new()
    {
        Id = id, CompanyId = 7, DistributorItemId = distributorItemId, GmkItemId = gmkItemId,
        GmkItemName = gmkItemName, DistributorItemName = distributorItemName, IsActive = active
    };

    private static byte[] Workbook(params (string GmkId, string GmkName, string DistributorId, string DistributorName,
        string Status)[] rows)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.AddWorksheet("Mappings");
        sheet.Cell(1, 1).Value = "GMK Item ID";
        sheet.Cell(1, 2).Value = "GMK Item Name";
        sheet.Cell(1, 3).Value = "Distributor Item ID";
        sheet.Cell(1, 4).Value = "Distributor Item Name";
        sheet.Cell(1, 5).Value = "Status";
        for (var index = 0; index < rows.Length; index++)
        {
            sheet.Cell(index + 2, 1).Value = rows[index].GmkId;
            sheet.Cell(index + 2, 2).Value = rows[index].GmkName;
            sheet.Cell(index + 2, 3).Value = rows[index].DistributorId;
            sheet.Cell(index + 2, 4).Value = rows[index].DistributorName;
            sheet.Cell(index + 2, 5).Value = rows[index].Status;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private sealed class FakeRepository(params ItemMapping[] mappings) : IItemMappingRepository
    {
        public List<ItemMapping> Mappings { get; } = [.. mappings];
        public List<ItemMapping> Upserted { get; } = [];

        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
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
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken)
        {
            Upserted.AddRange(values);
            return Task.CompletedTask;
        }
        public Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnmappedDistributorItem>>([]);
        public Task<IReadOnlyList<GmkItemSuggestion>> GetGmkItemSuggestionsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GmkItemSuggestion>>([]);
        public Task<IReadOnlyList<UnmappedItemCompanySummary>> GetUnmappedSummaryByCompanyAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnmappedItemCompanySummary>>([]);
    }

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
