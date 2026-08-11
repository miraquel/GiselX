using System.Data;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Tests;

public class CompanyItemMappingEnablementTests
{
    [Fact]
    public async Task UpdateCompanyAsync_rejects_enablement_without_active_mapping()
    {
        var companyRepository = new FakeCompanyRepository();
        var mappingRepository = new FakeItemMappingRepository();
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, mappingRepository, transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", ItemMappingEnabled = true }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("active item mapping", StringComparison.OrdinalIgnoreCase));
        Assert.False(companyRepository.UpdateCalled);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_enables_when_an_active_mapping_exists()
    {
        var companyRepository = new FakeCompanyRepository();
        var mappingRepository = new FakeItemMappingRepository
        {
            ActiveMappings = [new ItemMapping { Id = 3, CompanyId = 7, IsActive = true }]
        };
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, mappingRepository, transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", ItemMappingEnabled = true }, default);

        Assert.True(response.IsSuccess);
        Assert.True(companyRepository.UpdateCalled);
        Assert.True(companyRepository.UpdatedCompany!.ItemMappingEnabled);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_preserves_existing_enablement_when_request_uses_default_false()
    {
        var companyRepository = new FakeCompanyRepository
        {
            ExistingCompany = new Company { Id = 7, Name = "Distributor", ItemMappingEnabled = true }
        };
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto { Id = 7, Name = "Distributor" }, default);

        Assert.True(response.IsSuccess);
        Assert.True(companyRepository.UpdatedCompany!.ItemMappingEnabled);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_does_not_revalidate_an_already_enabled_company()
    {
        var companyRepository = new FakeCompanyRepository
        {
            ExistingCompany = new Company { Id = 7, Name = "Distributor", ItemMappingEnabled = true }
        };
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", ItemMappingEnabled = true }, default);

        Assert.True(response.IsSuccess);
        Assert.True(companyRepository.UpdateCalled);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_returns_not_found_before_enablement_validation()
    {
        var companyRepository = new FakeCompanyRepository { ExistingCompany = null };
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", ItemMappingEnabled = true }, default);

        Assert.Equal(404, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("not found", StringComparison.OrdinalIgnoreCase));
        Assert.False(companyRepository.UpdateCalled);
        Assert.False(transaction.CommitCalled);
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public bool UpdateCalled { get; private set; }
        public Company? UpdatedCompany { get; private set; }
        public Company? ExistingCompany { get; set; } = new() { Id = 7, Name = "Distributor" };

        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) => Task.FromResult(company);
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(ExistingCompany);
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedList<Company>([], 1, 10, 0));
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            UpdatedCompany = company;
            return Task.FromResult(company);
        }
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IEnumerable<Company>>([]);
    }

    private sealed class FakeItemMappingRepository : IItemMappingRepository
    {
        public IReadOnlyList<ItemMapping> ActiveMappings { get; set; } = [];
        public Task<IReadOnlyList<ItemMapping>> GetByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ItemMapping>>([]);
        public Task<IReadOnlyList<ItemMapping>> GetActiveByCompanyAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(ActiveMappings);
        public Task<ItemMapping?> GetByIdAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<ItemMapping?>(null);
        public Task<int> InsertAsync(ItemMapping mapping, CancellationToken cancellationToken) => Task.FromResult(0);
        public Task UpdateAsync(ItemMapping mapping, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<ItemMappingDeleteResult> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(ItemMappingDeleteResult.NotFound);
        public Task UpsertAsync(IReadOnlyList<ItemMapping> values, CancellationToken cancellationToken) => Task.CompletedTask;
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
