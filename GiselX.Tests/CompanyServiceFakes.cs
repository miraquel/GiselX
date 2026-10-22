using System.Data;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;

namespace GiselX.Tests;

/// <summary>Hand-rolled fakes for exercising CompanyService without a database.</summary>
internal static class CompanyServiceFakes
{
    internal sealed class RecordingCompanyRepository : ICompanyRepository
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

    internal sealed class NoItemMappings : IItemMappingRepository
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

    internal sealed class RecordingTransaction : IDbTransaction
    {
        public bool CommitCalled { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCalled = true;
        public void Rollback() { }
        public void Dispose() { }
    }
}
