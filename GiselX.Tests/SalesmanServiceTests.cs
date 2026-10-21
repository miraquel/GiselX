using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class SalesmanServiceTests
{
    [Fact]
    public async Task SaveAsync_creates_a_trimmed_salesman()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(new SalesmanDto
            { Code = "  SLS  ", Name = "  Budi Santoso  ", IsActive = false }, "admin", default);

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(repository.Inserted);
        Assert.Equal("SLS", saved.Code);
        Assert.Equal("Budi Santoso", saved.Name);
        Assert.False(saved.IsActive);
        Assert.Equal("admin", saved.CreatedBy);
        Assert.True(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_numbers_the_first_salesman_one()
    {
        var repository = new FakeRepository();
        var service = new SalesmanService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new SalesmanDto { Name = "Budi Santoso" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("1", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_continues_the_sequence_from_the_highest_existing_number()
    {
        var repository = new FakeRepository(
            new Salesman { Id = 1, Code = "1", Name = "Budi Santoso" },
            new Salesman { Id = 2, Code = "7", Name = "Siti Rahayu" },
            new Salesman { Id = 3, Code = "3", Name = "Andi Wijaya" });
        var service = new SalesmanService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new SalesmanDto { Name = "Rina Dewi" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("8", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_ignores_manually_overridden_codes_when_numbering()
    {
        var repository = new FakeRepository(
            new Salesman { Id = 1, Code = "SLS-SPECIAL", Name = "Budi Santoso" },
            new Salesman { Id = 2, Code = "2", Name = "Siti Rahayu" });
        var service = new SalesmanService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new SalesmanDto { Name = "Rina Dewi" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("3", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_keeps_a_code_the_administrator_supplied()
    {
        var repository = new FakeRepository(new Salesman { Id = 1, Code = "1", Name = "Budi Santoso" });
        var service = new SalesmanService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new SalesmanDto { Code = " SLS-99 ", Name = "Rina Dewi" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("SLS-99", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_will_not_invent_a_code_when_editing()
    {
        var repository = new FakeRepository(new Salesman { Id = 5, Code = "1", Name = "Budi Santoso" });
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(new SalesmanDto { Id = 5, Code = "  ", Name = "Budi Santoso" },
            "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task GetNextCodeAsync_previews_the_code_the_add_form_will_use()
    {
        var repository = new FakeRepository(new Salesman { Id = 1, Code = "4", Name = "Budi Santoso" });
        var service = new SalesmanService(repository, new FakeTransaction());

        Assert.Equal("5", (await service.GetNextCodeAsync(default)).Data);
    }

    [Fact]
    public async Task SaveAsync_rejects_a_case_insensitive_duplicate_code()
    {
        var repository = new FakeRepository(new Salesman { Id = 1, Code = "SLS", Name = "Budi Santoso" });
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(new SalesmanDto { Code = " sls ", Name = "Duplicate" }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_still_requires_a_name_even_though_the_code_is_generated()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(new SalesmanDto { Code = "  ", Name = "  " }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Salesman Name is required.", response.Errors);
        Assert.DoesNotContain(response.Errors, error => error.Contains("Code", StringComparison.Ordinal));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_maps_a_unique_index_violation_to_a_validation_error()
    {
        var repository = new FakeRepository { ThrowDuplicateOnInsert = true };
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(new SalesmanDto { Code = "SLS", Name = "Budi Santoso" }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_updates_an_existing_salesman_and_stamps_the_modifier()
    {
        var existing = new Salesman { Id = 5, Code = "SLS", Name = "Budi Santoso" };
        var repository = new FakeRepository(existing);
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.SaveAsync(
            new SalesmanDto { Id = 5, Code = "SLS2", Name = "Budi Santoso 2", IsActive = true }, "editor", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("SLS2", existing.Code);
        Assert.Equal("Budi Santoso 2", existing.Name);
        Assert.Equal("editor", existing.ModifiedBy);
        Assert.NotNull(existing.ModifiedDate);
        Assert.True(transaction.Committed);
    }

    [Fact]
    public async Task DeleteAsync_blocks_a_salesman_assigned_to_a_company()
    {
        var repository = new FakeRepository { DeleteResult = SalesmanDeleteResult.InUse };
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.DeleteAsync(1, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task DeleteAsync_reports_not_found_without_committing()
    {
        var repository = new FakeRepository { DeleteResult = SalesmanDeleteResult.NotFound };
        var transaction = new FakeTransaction();
        var service = new SalesmanService(repository, transaction);

        var response = await service.DeleteAsync(1, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(404, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task ListAsync_can_return_active_salesmans_only()
    {
        var repository = new FakeRepository(
            new Salesman { Id = 1, Code = "SLS", Name = "Budi Santoso" },
            new Salesman { Id = 2, Code = "GT1", Name = "Siti Rahayu", IsActive = false });
        var service = new SalesmanService(repository, new FakeTransaction());

        var response = await service.ListAsync(true, default);

        Assert.Equal("SLS", Assert.Single(response.Data!).Code);
    }

    private sealed class FakeRepository(params Salesman[] values) : ISalesmanRepository
    {
        public List<Salesman> Values { get; } = [.. values];
        public List<Salesman> Inserted { get; } = [];
        public SalesmanDeleteResult DeleteResult { get; set; } = SalesmanDeleteResult.Deleted;
        public bool ThrowDuplicateOnInsert { get; set; }

        public Task<IReadOnlyList<Salesman>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Salesman>>(Values.Where(value => !activeOnly || value.IsActive).ToList());

        public Task<Salesman?> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(value => value.Id == id));

        public Task<int> InsertAsync(Salesman salesman, CancellationToken cancellationToken)
        {
            if (ThrowDuplicateOnInsert) throw new SalesmanDuplicateException(new InvalidOperationException("2627"));
            Inserted.Add(salesman);
            return Task.FromResult(42);
        }

        public Task UpdateAsync(Salesman salesman, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<SalesmanDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken) =>
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
