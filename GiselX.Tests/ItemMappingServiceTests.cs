using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using Microsoft.AspNetCore.Http;

namespace GiselX.Tests;

public class ItemMappingServiceTests
{
    [Fact]
    public async Task SaveAsync_RejectsMissingRequiredFields()
    {
        var dto = ValidSave();
        dto.GmkItemId = " ";
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("GMK Item ID"));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_rejects_trimmed_case_insensitive_duplicate()
    {
        var repository = new FakeItemMappingRepository(new ItemMapping
            { Id = 3, CompanyId = 7, DistributorItemId = " p0003 " });
        var transaction = new FakeTransaction();
        var service = new ItemMappingService(repository, transaction);
        var dto = ValidSave();
        dto.DistributorItemId = "P0003";

        var response = await service.SaveAsync(dto, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("Distributor Item ID"));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_trims_all_values_and_commits()
    {
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(ValidSave(), "admin", default);

        var saved = Assert.Single(repository.Inserted);
        Assert.Equal("4100039", saved.GmkItemId);
        Assert.Equal("Panadol", saved.GmkItemName);
        Assert.Equal("P0003", saved.DistributorItemId);
        Assert.Equal("Panadol Extra", saved.DistributorItemName);
        Assert.Equal("admin", saved.CreatedBy);
        Assert.InRange(saved.CreatedDate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(transaction.CommitCalled);
        Assert.Equal(42, response.Data);
    }

    [Fact]
    public async Task SaveAsync_updates_owned_mapping_with_audit()
    {
        var (service, repository, transaction) = CreateService();
        var mapping = new ItemMapping { Id = 8, CompanyId = 7, GmkItemId = "old", GmkItemName = "old", DistributorItemId = "old", DistributorItemName = "old" };
        repository.Mappings.Add(mapping);
        var dto = ValidSave();
        dto.Id = 8;

        var response = await service.SaveAsync(dto, "editor", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Updated);
        Assert.Equal("editor", mapping.ModifiedBy);
        Assert.InRange(mapping.ModifiedDate!.Value, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_rejects_mapping_owned_by_another_company_without_commit()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new ItemMapping { Id = 8, CompanyId = 99 });
        var dto = ValidSave();
        dto.Id = 8;

        var response = await service.SaveAsync(dto, "editor", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Empty(repository.Updated);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_updates_requested_state_and_audit_then_commits()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new ItemMapping { Id = 7, CompanyId = 7 });

        var response = await service.SetActiveAsync(7, 7, false, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        var change = Assert.IsType<ActiveChange>(repository.ActiveChange);
        Assert.Equal(7, change.Id);
        Assert.Equal(7, change.CompanyId);
        Assert.False(change.IsActive);
        Assert.Equal("admin", change.ModifiedBy);
        Assert.InRange(change.ModifiedDate, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SetActiveAsync_rejects_mapping_from_another_company_without_commit()
    {
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new ItemMapping { Id = 7, CompanyId = 2 });

        var response = await service.SetActiveAsync(7, 7, true, "admin", CancellationToken.None);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(repository.ActiveChange);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task DeleteAsync_deletes_unused_mapping_and_commits()
    {
        var (service, repository, transaction) = CreateService();
        repository.DeleteResult = ItemMappingDeleteResult.Deleted;

        var response = await service.DeleteAsync(7, 7, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Equal((7, 7), repository.DeletedMapping);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task DeleteAsync_blocks_mapping_with_upload_history_without_commit()
    {
        var (service, repository, transaction) = CreateService();
        repository.DeleteResult = ItemMappingDeleteResult.HasUploadHistory;

        var response = await service.DeleteAsync(7, 7, CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("upload history", StringComparison.OrdinalIgnoreCase));
        Assert.False(transaction.CommitCalled);
    }

    private static (ItemMappingService Service, FakeItemMappingRepository Repository, FakeTransaction Transaction) CreateService()
    {
        var repository = new FakeItemMappingRepository();
        var transaction = new FakeTransaction();
        return (new ItemMappingService(repository, transaction), repository, transaction);
    }

    private static SaveItemMappingDto ValidSave() => new()
    {
        CompanyId = 7,
        GmkItemId = " 4100039 ",
        GmkItemName = " Panadol ",
        DistributorItemId = " P0003 ",
        DistributorItemName = " Panadol Extra "
    };

    private sealed class FakeItemMappingRepository(params ItemMapping[] mappings) : IItemMappingRepository
    {
        public List<ItemMapping> Mappings { get; } = [.. mappings];
        public List<ItemMapping> Inserted { get; } = [];
        public List<ItemMapping> Updated { get; } = [];
        public ItemMappingDeleteResult DeleteResult { get; set; }
        public (int Id, int CompanyId)? DeletedMapping { get; private set; }
        public ActiveChange? ActiveChange { get; private set; }

        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId).ToList());
        public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>(Mappings.Where(mapping => mapping.CompanyId == companyId && mapping.IsActive).ToList());
        public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Mappings.SingleOrDefault(mapping => mapping.Id == id && mapping.CompanyId == companyId));
        public Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken)
        {
            Inserted.Add(mapping);
            return Task.FromResult(42);
        }
        public Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken)
        {
            Updated.Add(mapping);
            return Task.CompletedTask;
        }
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate, CancellationToken cancellationToken)
        {
            ActiveChange = new ActiveChange(id, companyId, isActive, modifiedBy, modifiedDate);
            return Task.CompletedTask;
        }
        public Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken)
        {
            DeletedMapping = (id, companyId);
            return Task.FromResult(DeleteResult);
        }
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed record ActiveChange(int Id, int CompanyId, bool IsActive, string ModifiedBy, DateTime ModifiedDate);

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
