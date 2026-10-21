using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class StockAreaServiceTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Name_is_required_and_at_most_100_characters(string name)
    {
        var (service, repository, _) = Create();

        var blank = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = name }, "admin", default);
        var tooLong = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = new string('a', 101) }, "admin", default);
        var exact = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = new string('a', 100) }, "admin", default);

        Assert.False(blank.IsSuccess);
        Assert.False(tooLong.IsSuccess);
        Assert.True(exact.IsSuccess);
        Assert.Single(repository.Inserted);
    }

    [Fact]
    public async Task Aliases_are_split_trimmed_and_deduplicated_before_storing()
    {
        var (service, repository, _) = Create();

        var response = await service.SaveAsync(new StockAreaDto
            { CompanyId = 1, Name = "  Medan   Kota ", Aliases = " MDN ;mdn;; Gdg Medan " }, "admin", default);

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(repository.Inserted);
        Assert.Equal("MDN;Gdg Medan", saved.Aliases);
        Assert.Equal("Medan Kota", saved.Name);

        await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai", Aliases = " ; " }, "admin", default);
        Assert.Null(repository.Inserted[1].Aliases);
    }

    [Fact]
    public async Task An_alias_equal_to_another_active_area_name_or_alias_is_rejected()
    {
        var (service, repository, transaction) = Create(
            new StockArea { Id = 1, CompanyId = 1, Name = "Medan", Aliases = "MDN" },
            new StockArea { Id = 2, CompanyId = 1, Name = "Old", Aliases = "OLD1", IsActive = false });

        var viaAlias = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai", Aliases = "mdn" }, "admin", default);
        var viaName = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai", Aliases = "medan" }, "admin", default);
        var viaInactive = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai", Aliases = "old1" }, "admin", default);

        Assert.False(viaAlias.IsSuccess);
        Assert.Contains("'mdn'", viaAlias.Errors[0]);
        Assert.Contains("'Medan'", viaAlias.Errors[0]);
        Assert.False(viaName.IsSuccess);
        Assert.False(viaInactive.IsSuccess);
        Assert.Contains("'Old'", viaInactive.Errors[0]);
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.Committed);

        // An alias equal to the area's own name is merely redundant.
        var ownName = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai", Aliases = "binjai" }, "admin", default);
        Assert.True(ownName.IsSuccess);
    }

    [Fact]
    public async Task A_duplicate_name_in_the_same_company_is_rejected_but_allowed_in_another_company()
    {
        var (service, repository, _) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan" });

        var same = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = " medan " }, "admin", default);
        var other = await service.SaveAsync(new StockAreaDto { CompanyId = 2, Name = "Medan", Aliases = "MDN" }, "admin", default);

        Assert.False(same.IsSuccess);
        Assert.Contains("already exists", same.Errors[0]);
        Assert.True(other.IsSuccess);
        Assert.Equal(2, Assert.Single(repository.Inserted).CompanyId);
    }

    [Fact]
    public async Task An_area_referenced_by_stock_cannot_be_deleted()
    {
        var (service, repository, transaction) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan" });
        repository.DeleteResult = false;

        var response = await service.DeleteAsync(1, 1, default);

        Assert.Equal(409, response.StatusCode);
        Assert.Equal("This area has stock rows and cannot be deleted. Deactivate it instead.", response.Errors[0]);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task Another_companys_area_is_not_found()
    {
        var (service, _, transaction) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan" });

        var response = await service.DeleteAsync(1, 2, default);

        Assert.Equal(404, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task Saving_commits_the_transaction()
    {
        var (service, _, transaction) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan" });

        var insert = await service.SaveAsync(new StockAreaDto { CompanyId = 1, Name = "Binjai" }, "admin", default);
        Assert.True(insert.IsSuccess);
        Assert.True(transaction.Committed);

        var (service2, repository2, transaction2) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan" });
        var update = await service2.SaveAsync(new StockAreaDto { Id = 1, CompanyId = 1, Name = "Medan Kota", IsActive = false }, "admin", default);
        Assert.True(update.IsSuccess);
        Assert.True(transaction2.Committed);
        Assert.Equal("Medan Kota", repository2.Values[0].Name);
        Assert.False(repository2.Values[0].IsActive);
    }

    [Fact]
    public async Task An_area_can_keep_its_own_name_and_aliases_when_edited_and_deactivating_commits()
    {
        var (service, _, transaction) = Create(new StockArea { Id = 1, CompanyId = 1, Name = "Medan", Aliases = "MDN" });

        var edit = await service.SaveAsync(new StockAreaDto { Id = 1, CompanyId = 1, Name = "Medan", Aliases = "MDN;MDN2" }, "admin", default);
        var off = await service.SetActiveAsync(1, 1, false, "admin", default);

        Assert.True(edit.IsSuccess);
        Assert.True(off.IsSuccess);
        Assert.True(transaction.Committed);
    }

    private static (StockAreaService, FakeRepository, FakeTransaction) Create(params StockArea[] areas)
    {
        var repository = new FakeRepository(areas);
        var transaction = new FakeTransaction();
        return (new StockAreaService(repository, transaction), repository, transaction);
    }

    private sealed class FakeRepository(params StockArea[] values) : IStockAreaRepository
    {
        public List<StockArea> Values { get; } = [.. values];
        public List<StockArea> Inserted { get; } = [];
        public bool DeleteResult { get; set; } = true;

        public Task<IReadOnlyList<StockArea>> ListAsync(int companyId, bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StockArea>>(Values
                .Where(v => v.CompanyId == companyId && (!activeOnly || v.IsActive)).ToList());
        public Task<StockArea?> GetAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(v => v.Id == id && v.CompanyId == companyId));
        public Task<int> InsertAsync(StockArea area, CancellationToken cancellationToken)
        {
            Inserted.Add(area);
            return Task.FromResult(42);
        }
        public Task UpdateAsync(StockArea area, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task SetActiveAsync(int id, int companyId, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;
        public Task<bool> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
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
