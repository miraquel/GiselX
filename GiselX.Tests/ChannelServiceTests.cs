using System.Data;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class ChannelServiceTests
{
    [Fact]
    public async Task SaveAsync_creates_a_trimmed_channel()
    {
        var repository = new FakeRepository();
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(new ChannelDto
            { Code = "  MT  ", Name = "  Modern Trade  ", IsActive = false }, "admin", default);

        Assert.True(response.IsSuccess);
        var saved = Assert.Single(repository.Inserted);
        Assert.Equal("MT", saved.Code);
        Assert.Equal("Modern Trade", saved.Name);
        Assert.False(saved.IsActive);
        Assert.Equal("admin", saved.CreatedBy);
        Assert.True(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_numbers_the_first_channel_one()
    {
        var repository = new FakeRepository();
        var service = new ChannelService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new ChannelDto { Name = "Modern Trade" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("1", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_continues_the_sequence_from_the_highest_existing_number()
    {
        var repository = new FakeRepository(
            new Channel { Id = 1, Code = "1", Name = "Modern Trade" },
            new Channel { Id = 2, Code = "7", Name = "General Trade" },
            new Channel { Id = 3, Code = "3", Name = "Horeca" });
        var service = new ChannelService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new ChannelDto { Name = "Online" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("8", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_ignores_manually_overridden_codes_when_numbering()
    {
        var repository = new FakeRepository(
            new Channel { Id = 1, Code = "MT-SPECIAL", Name = "Modern Trade" },
            new Channel { Id = 2, Code = "2", Name = "General Trade" });
        var service = new ChannelService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new ChannelDto { Name = "Online" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("3", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_keeps_a_code_the_administrator_supplied()
    {
        var repository = new FakeRepository(new Channel { Id = 1, Code = "1", Name = "Modern Trade" });
        var service = new ChannelService(repository, new FakeTransaction());

        var response = await service.SaveAsync(new ChannelDto { Code = " MT-99 ", Name = "Online" }, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("MT-99", Assert.Single(repository.Inserted).Code);
    }

    [Fact]
    public async Task SaveAsync_will_not_invent_a_code_when_editing()
    {
        var repository = new FakeRepository(new Channel { Id = 5, Code = "1", Name = "Modern Trade" });
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(new ChannelDto { Id = 5, Code = "  ", Name = "Modern Trade" },
            "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("required", StringComparison.OrdinalIgnoreCase));
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task GetNextCodeAsync_previews_the_code_the_add_form_will_use()
    {
        var repository = new FakeRepository(new Channel { Id = 1, Code = "4", Name = "Modern Trade" });
        var service = new ChannelService(repository, new FakeTransaction());

        Assert.Equal("5", (await service.GetNextCodeAsync(default)).Data);
    }

    [Fact]
    public async Task SaveAsync_rejects_a_case_insensitive_duplicate_code()
    {
        var repository = new FakeRepository(new Channel { Id = 1, Code = "MT", Name = "Modern Trade" });
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(new ChannelDto { Code = " mt ", Name = "Duplicate" }, "admin", default);

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
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(new ChannelDto { Code = "  ", Name = "  " }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
        Assert.Contains("Channel Name is required.", response.Errors);
        Assert.DoesNotContain(response.Errors, error => error.Contains("Code", StringComparison.Ordinal));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_maps_a_unique_index_violation_to_a_validation_error()
    {
        var repository = new FakeRepository { ThrowDuplicateOnInsert = true };
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(new ChannelDto { Code = "MT", Name = "Modern Trade" }, "admin", default);

        Assert.False(response.IsSuccess);
        Assert.Equal(400, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task SaveAsync_updates_an_existing_channel_and_stamps_the_modifier()
    {
        var existing = new Channel { Id = 5, Code = "MT", Name = "Modern Trade" };
        var repository = new FakeRepository(existing);
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.SaveAsync(
            new ChannelDto { Id = 5, Code = "MT2", Name = "Modern Trade 2", IsActive = true }, "editor", default);

        Assert.True(response.IsSuccess);
        Assert.Equal("MT2", existing.Code);
        Assert.Equal("Modern Trade 2", existing.Name);
        Assert.Equal("editor", existing.ModifiedBy);
        Assert.NotNull(existing.ModifiedDate);
        Assert.True(transaction.Committed);
    }

    [Fact]
    public async Task DeleteAsync_blocks_a_channel_assigned_to_a_company()
    {
        var repository = new FakeRepository { DeleteResult = ChannelDeleteResult.InUse };
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.DeleteAsync(1, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(409, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task DeleteAsync_reports_not_found_without_committing()
    {
        var repository = new FakeRepository { DeleteResult = ChannelDeleteResult.NotFound };
        var transaction = new FakeTransaction();
        var service = new ChannelService(repository, transaction);

        var response = await service.DeleteAsync(1, default);

        Assert.False(response.IsSuccess);
        Assert.Equal(404, response.StatusCode);
        Assert.False(transaction.Committed);
    }

    [Fact]
    public async Task ListAsync_can_return_active_channels_only()
    {
        var repository = new FakeRepository(
            new Channel { Id = 1, Code = "MT", Name = "Modern Trade" },
            new Channel { Id = 2, Code = "GT", Name = "General Trade", IsActive = false });
        var service = new ChannelService(repository, new FakeTransaction());

        var response = await service.ListAsync(true, default);

        Assert.Equal("MT", Assert.Single(response.Data!).Code);
    }

    private sealed class FakeRepository(params Channel[] values) : IChannelRepository
    {
        public List<Channel> Values { get; } = [.. values];
        public List<Channel> Inserted { get; } = [];
        public ChannelDeleteResult DeleteResult { get; set; } = ChannelDeleteResult.Deleted;
        public bool ThrowDuplicateOnInsert { get; set; }

        public Task<IReadOnlyList<Channel>> ListAsync(bool activeOnly, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Channel>>(Values.Where(value => !activeOnly || value.IsActive).ToList());

        public Task<Channel?> GetAsync(int id, CancellationToken cancellationToken) =>
            Task.FromResult(Values.SingleOrDefault(value => value.Id == id));

        public Task<int> InsertAsync(Channel channel, CancellationToken cancellationToken)
        {
            if (ThrowDuplicateOnInsert) throw new ChannelDuplicateException(new InvalidOperationException("2627"));
            Inserted.Add(channel);
            return Task.FromResult(42);
        }

        public Task UpdateAsync(Channel channel, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ChannelDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken) =>
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
