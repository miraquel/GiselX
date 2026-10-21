using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class BusinessUnitServiceTests
{
    [Fact]
    public async Task SaveAsync_creates_trimmed_inactive_business_unit()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        var service = new BusinessUnitService(repository, transaction);

        var response = await service.SaveAsync(new BusinessUnitDto
            { Name = "  Consumer Health  ", IsActive = false }, "admin", default);

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(repository.Inserted);
        Assert.Equal("Consumer Health", saved.Name);
        Assert.False(saved.IsActive);
        Assert.Equal("admin", saved.CreatedBy);
        Assert.True(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_rejects_case_insensitive_duplicate()
    {
        var repository = new FakeRepository(new BusinessUnit { Id = 1, Name = "Prescription" });
        var transaction = new FakeTransaction();
        var service = new BusinessUnitService(repository, transaction);

        var response = await service.SaveAsync(new BusinessUnitDto { Name = " prescription " }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task DeleteAsync_blocks_business_unit_assigned_to_a_company()
    {
        var repository = new FakeRepository { DeleteResult = BusinessUnitDeleteResult.InUse };
        var transaction = new FakeTransaction();
        var service = new BusinessUnitService(repository, transaction);

        var response = await service.DeleteAsync(1, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    private sealed class FakeRepository(params BusinessUnit[] values) : IBusinessUnitRepository
    {
        public List<BusinessUnit> Values { get; } = [.. values];
        public List<BusinessUnit> Inserted { get; } = [];
        public BusinessUnitDeleteResult DeleteResult { get; set; } = BusinessUnitDeleteResult.Deleted;

        public Task<IReadOnlyList<BusinessUnit>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<BusinessUnit>>(Values.Where(value => !activeOnly || value.IsActive).ToList());
        public Task<BusinessUnit?> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(value => value.Id == id));
        public Task<int> InsertAsync(BusinessUnit businessUnit, CancellationToken cancellationToken)
        {
            Inserted.Add(businessUnit);
            return Task.FromResult(42);
        }
        public Task UpdateAsync(BusinessUnit businessUnit, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<BusinessUnitDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(DeleteResult);
    }

    private sealed class FakeTransaction : IDbTransaction
    {
        public bool Committed { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => Committed = true;
        public void Dispose() { }
        public void Rollback() { }
    }
}
