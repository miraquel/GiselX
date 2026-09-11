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
    public async Task CreateCompanyAsync_maps_business_unit_to_the_company()
    {
        var companyRepository = new FakeCompanyRepository();
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction,
            new UserClaimDto());

        var response = await service.CreateCompanyAsync(new CompanyDto
            { Name = "Distributor", BusinessUnitId = 3, BusinessUnitName = "Consumer Health" }, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(3, companyRepository.CreatedCompany!.BusinessUnitId);
        Assert.Equal("Consumer Health", response.Data!.BusinessUnitName);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task CreateCompanyAsync_rejects_an_inactive_business_unit()
    {
        var companyRepository = new FakeCompanyRepository();
        var transaction = new FakeTransaction();
        var businessUnits = new FakeBusinessUnitRepository
        {
            Values = [new BusinessUnit { Id = 3, Name = "Consumer Health", IsActive = false }]
        };
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction,
            new UserClaimDto(), businessUnits);

        var response = await service.CreateCompanyAsync(new CompanyDto
            { Name = "Distributor", BusinessUnitId = 3 }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("active Business Unit", StringComparison.Ordinal));
        Assert.False(companyRepository.CreateCalled);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_maps_business_unit_to_the_company()
    {
        var companyRepository = new FakeCompanyRepository();
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction,
            new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", BusinessUnitId = 4, BusinessUnitName = "Prescription" }, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(4, companyRepository.UpdatedCompany!.BusinessUnitId);
        Assert.Equal("Prescription", response.Data!.BusinessUnitName);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpdateCompanyAsync_allows_an_existing_inactive_business_unit_to_remain_selected()
    {
        var companyRepository = new FakeCompanyRepository
        {
            ExistingCompany = new Company { Id = 7, Name = "Distributor", BusinessUnitId = 3 }
        };
        var transaction = new FakeTransaction();
        var businessUnits = new FakeBusinessUnitRepository
        {
            Values = [new BusinessUnit { Id = 3, Name = "Consumer Health", IsActive = false }]
        };
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction,
            new UserClaimDto(), businessUnits);

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", BusinessUnitId = 3 }, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(3, companyRepository.UpdatedCompany!.BusinessUnitId);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task CreateCompanyAsync_rejects_explicit_item_mapping_enablement()
    {
        var companyRepository = new FakeCompanyRepository();
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction, new UserClaimDto());

        var response = await service.CreateCompanyAsync(new CompanyDto
            { Name = "Distributor", ItemMappingEnabled = true }, default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("active item mapping", StringComparison.OrdinalIgnoreCase));
        Assert.False(companyRepository.CreateCalled);
        Assert.False(transaction.CommitCalled);
    }

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
    public async Task UpdateCompanyAsync_preserves_existing_enablement_when_item_mapping_state_is_omitted()
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
    public async Task UpdateCompanyAsync_disables_existing_enablement_when_item_mapping_state_is_explicit_false()
    {
        var companyRepository = new FakeCompanyRepository
        {
            ExistingCompany = new Company { Id = 7, Name = "Distributor", ItemMappingEnabled = true }
        };
        var transaction = new FakeTransaction();
        var service = new CompanyService(companyRepository, new FakeItemMappingRepository(), transaction, new UserClaimDto());

        var response = await service.UpdateCompanyAsync(new CompanyDto
            { Id = 7, Name = "Distributor", ItemMappingEnabled = false }, default);

        Assert.True(response.IsSuccess);
        Assert.False(companyRepository.UpdatedCompany!.ItemMappingEnabled);
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
        public bool CreateCalled { get; private set; }
        public Company? CreatedCompany { get; private set; }
        public bool UpdateCalled { get; private set; }
        public Company? UpdatedCompany { get; private set; }
        public Company? ExistingCompany { get; set; } = new() { Id = 7, Name = "Distributor" };

        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken)
        {
            CreateCalled = true;
            CreatedCompany = company;
            return Task.FromResult(company);
        }
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(ExistingCompany);
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) =>
            Task.FromResult(new PagedList<Company>([], 1, 10, 0));
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken)
        {
            UpdateCalled = true;
            UpdatedCompany = company;
            return Task.FromResult(company);
        }
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Company>>([]);
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
        public Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnmappedDistributorItem>>(UnmappedItems);
        public List<UnmappedDistributorItem> UnmappedItems { get; } = [];
    }

    private sealed class FakeBusinessUnitRepository : IBusinessUnitRepository
    {
        public IReadOnlyList<BusinessUnit> Values { get; init; } = [];

        public Task<IReadOnlyList<BusinessUnit>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessUnit>>(Values.Where(value => !activeOnly || value.IsActive).ToList());
        public Task<BusinessUnit?> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
        public Task<int> InsertAsync(BusinessUnit businessUnit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task UpdateAsync(BusinessUnit businessUnit, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
        public Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<BusinessUnitDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
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
