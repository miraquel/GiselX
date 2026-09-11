using System.Data;
using System.Collections;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository;
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
    public async Task SaveAsync_rejects_null_distributor_item_id_without_throwing()
    {
        var dto = ValidSave();
        dto.DistributorItemId = null!;
        var (service, repository, transaction) = CreateService();
        repository.Mappings.Add(new ItemMapping { Id = 8, CompanyId = 7, DistributorItemId = "OTHER" });

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("Distributor Item ID"));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Theory]
    [InlineData(nameof(SaveItemMappingDto.GmkItemId), 100)]
    [InlineData(nameof(SaveItemMappingDto.GmkItemName), 200)]
    [InlineData(nameof(SaveItemMappingDto.DistributorItemId), 100)]
    [InlineData(nameof(SaveItemMappingDto.DistributorItemName), 200)]
    public async Task SaveAsync_accepts_maximum_trimmed_field_length(string field, int length)
    {
        var dto = ValidSave();
        SetField(dto, field, new string('X', length));
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Inserted);
        Assert.True(transaction.CommitCalled);
    }

    [Theory]
    [InlineData(nameof(SaveItemMappingDto.GmkItemId), 101, "GMK Item ID")]
    [InlineData(nameof(SaveItemMappingDto.GmkItemName), 201, "GMK Item Name")]
    [InlineData(nameof(SaveItemMappingDto.DistributorItemId), 101, "Distributor Item ID")]
    [InlineData(nameof(SaveItemMappingDto.DistributorItemName), 201, "Distributor Item Name")]
    public async Task SaveAsync_rejects_overlength_trimmed_field(string field, int length, string expectedField)
    {
        var dto = ValidSave();
        SetField(dto, field, new string('X', length));
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains(expectedField));
        Assert.Empty(repository.Inserted);
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_translates_duplicate_key_conflict_to_distributor_item_id_error()
    {
        var (service, repository, transaction) = CreateService();
        repository.InsertException = new ItemMappingDuplicateException();

        var response = await service.SaveAsync(ValidSave(), "admin", CancellationToken.None);

        Assert.False(response.IsSuccess);
        Assert.Contains(response.Errors, error => error.Contains("Distributor Item ID"));
        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_propagates_non_duplicate_persistence_errors()
    {
        var (service, repository, transaction) = CreateService();
        repository.InsertException = new InvalidOperationException("Database unavailable.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.SaveAsync(ValidSave(), "admin", CancellationToken.None));

        Assert.False(transaction.CommitCalled);
    }

    [Fact]
    public async Task UpsertAsync_emits_normalized_match_and_updates_activation()
    {
        var connection = new RecordingDbConnection();
        var repository = new ItemMappingRepository(connection, null!);

        await repository.UpsertAsync([new ItemMapping
        {
            CompanyId = 7,
            GmkItemId = "4100039",
            GmkItemName = "Panadol",
            DistributorItemId = " P0003 ",
            DistributorItemName = "Panadol Extra",
            IsActive = false,
            CreatedBy = "upload"
        }], CancellationToken.None);

        Assert.Contains("UPPER(LTRIM(RTRIM(target.DistributorItemId))) = UPPER(LTRIM(RTRIM(source.DistributorItemId)))",
            connection.LastCommandText);
        var matchedUpdate = connection.LastCommandText.Split("WHEN NOT MATCHED", StringSplitOptions.None)[0];
        Assert.Contains("IsActive = source.IsActive", matchedUpdate);
        Assert.Contains("source.IsActive", connection.LastCommandText.Split("WHEN NOT MATCHED", StringSplitOptions.None)[1]);
    }

    [Fact]
    public async Task SaveAsync_rejects_trimmed_case_insensitive_duplicate()
    {
        var repository = new FakeItemMappingRepository(new ItemMapping
            { Id = 3, CompanyId = 7, DistributorItemId = " p0003 " });
        var transaction = new FakeTransaction();
        var service = new ItemMappingService(repository, new FakeCompanyRepository(), transaction);
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
    public async Task SaveAsync_creates_mapping_with_selected_inactive_status()
    {
        var dto = ValidSave();
        dto.IsActive = false;
        var (service, repository, transaction) = CreateService();

        var response = await service.SaveAsync(dto, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.False(Assert.Single(repository.Inserted).IsActive);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task SaveAsync_updates_owned_mapping_with_audit()
    {
        var (service, repository, transaction) = CreateService();
        var mapping = new ItemMapping { Id = 8, CompanyId = 7, GmkItemId = "old", GmkItemName = "old", DistributorItemId = "old", DistributorItemName = "old" };
        repository.Mappings.Add(mapping);
        var dto = ValidSave();
        dto.Id = 8;
        dto.IsActive = false;

        var response = await service.SaveAsync(dto, "editor", CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Updated);
        Assert.False(mapping.IsActive);
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

    [Fact]
    public async Task ListUnmappedAsync_returns_discovered_items_for_a_mapping_enabled_company()
    {
        var (service, repository) = CreateUnmappedService(itemMappingEnabled: true);
        repository.UnmappedItems.Add(new UnmappedDistributorItem
        {
            DistributorItemId = "P0003",
            DistributorItemName = "Panadol Extra",
            SeenInStock = true,
            SeenInSellout = true,
            OccurrenceCount = 12,
            FirstSeenDate = new DateTime(2026, 8, 1, 3, 0, 0, DateTimeKind.Utc),
            LastSeenDate = new DateTime(2026, 9, 3, 4, 0, 0, DateTimeKind.Utc)
        });

        var response = await service.ListUnmappedAsync(7, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.True(response.Data!.ItemMappingEnabled);
        var item = Assert.Single(response.Data.Items);
        Assert.Equal("P0003", item.DistributorItemId);
        Assert.Equal("Panadol Extra", item.DistributorItemName);
        Assert.True(item.SeenInStock);
        Assert.True(item.SeenInSellout);
        Assert.Equal(12, item.OccurrenceCount);
        Assert.Equal(new DateTime(2026, 9, 3, 4, 0, 0, DateTimeKind.Utc), item.LastSeenDate);
    }

    [Fact]
    public async Task ListUnmappedAsync_stays_empty_when_item_mapping_is_disabled_for_the_company()
    {
        var (service, repository) = CreateUnmappedService(itemMappingEnabled: false);
        repository.UnmappedItems.Add(new UnmappedDistributorItem { DistributorItemId = "P0003" });

        var response = await service.ListUnmappedAsync(7, CancellationToken.None);

        Assert.True(response.IsSuccess);
        Assert.False(response.Data!.ItemMappingEnabled);
        Assert.Empty(response.Data.Items);
    }

    [Fact]
    public async Task ListUnmappedAsync_stays_empty_when_the_company_does_not_exist()
    {
        var repository = new FakeItemMappingRepository();
        var companyRepository = new FakeCompanyRepository { Company = null };
        var service = new ItemMappingService(repository, companyRepository, new FakeTransaction());
        repository.UnmappedItems.Add(new UnmappedDistributorItem { DistributorItemId = "P0003" });

        var response = await service.ListUnmappedAsync(7, CancellationToken.None);

        Assert.False(response.Data!.ItemMappingEnabled);
        Assert.Empty(response.Data.Items);
    }

    [Fact]
    public async Task ListUnmappedAsync_maps_a_null_distributor_item_name_to_an_empty_string()
    {
        var (service, repository) = CreateUnmappedService(itemMappingEnabled: true);
        repository.UnmappedItems.Add(new UnmappedDistributorItem { DistributorItemId = "P0003", DistributorItemName = null });

        var response = await service.ListUnmappedAsync(7, CancellationToken.None);

        Assert.Equal(string.Empty, Assert.Single(response.Data!.Items).DistributorItemName);
    }

    private static (ItemMappingService Service, FakeItemMappingRepository Repository) CreateUnmappedService(
        bool itemMappingEnabled)
    {
        var repository = new FakeItemMappingRepository();
        var companyRepository = new FakeCompanyRepository
        {
            Company = new Company { Id = 7, ItemMappingEnabled = itemMappingEnabled }
        };
        return (new ItemMappingService(repository, companyRepository, new FakeTransaction()), repository);
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public Company? Company { get; set; } = new() { Id = 7, ItemMappingEnabled = true };
        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) => Task.FromResult(Company);
        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Company>>([]);
        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private static (ItemMappingService Service, FakeItemMappingRepository Repository, FakeTransaction Transaction) CreateService()
    {
        var repository = new FakeItemMappingRepository();
        var transaction = new FakeTransaction();
        return (new ItemMappingService(repository, new FakeCompanyRepository(), transaction), repository, transaction);
    }

    private static SaveItemMappingDto ValidSave() => new()
    {
        CompanyId = 7,
        GmkItemId = " 4100039 ",
        GmkItemName = " Panadol ",
        DistributorItemId = " P0003 ",
        DistributorItemName = " Panadol Extra "
    };

    private static void SetField(SaveItemMappingDto dto, string field, string value)
    {
        switch (field)
        {
            case nameof(SaveItemMappingDto.GmkItemId): dto.GmkItemId = value; break;
            case nameof(SaveItemMappingDto.GmkItemName): dto.GmkItemName = value; break;
            case nameof(SaveItemMappingDto.DistributorItemId): dto.DistributorItemId = value; break;
            case nameof(SaveItemMappingDto.DistributorItemName): dto.DistributorItemName = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }

    private sealed class FakeItemMappingRepository(params ItemMapping[] mappings) : IItemMappingRepository
    {
        public List<ItemMapping> Mappings { get; } = [.. mappings];
        public List<ItemMapping> Inserted { get; } = [];
        public List<ItemMapping> Updated { get; } = [];
        public Exception? InsertException { get; set; }
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
            if (InsertException is not null) throw InsertException;
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
        public Task<IReadOnlyList<UnmappedDistributorItem>> GetUnmappedDistributorItemsAsync(int companyId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<UnmappedDistributorItem>>(UnmappedItems);
        public List<UnmappedDistributorItem> UnmappedItems { get; } = [];
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

    private sealed class RecordingDbConnection : DbConnection
    {
        public string LastCommandText { get; set; } = string.Empty;
        [AllowNull] public override string ConnectionString { get; set; } = string.Empty;
        public override string Database => "Recording";
        public override string DataSource => "Recording";
        public override string ServerVersion => "1";
        public override ConnectionState State => ConnectionState.Open;
        public override int ConnectionTimeout => 0;
        public override void ChangeDatabase(string databaseName) { }
        public override void Close() { }
        public override void Open() { }
        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) => throw new NotSupportedException();
        protected override DbCommand CreateDbCommand() => new RecordingDbCommand(this);
    }

    private sealed class RecordingDbCommand(RecordingDbConnection connection) : DbCommand
    {
        [AllowNull] public override string CommandText { get; set; } = string.Empty;
        public override int CommandTimeout { get; set; }
        public override CommandType CommandType { get; set; }
        public override bool DesignTimeVisible { get; set; }
        public override UpdateRowSource UpdatedRowSource { get; set; }
        protected override DbConnection? DbConnection { get; set; } = connection;
        protected override DbTransaction? DbTransaction { get; set; }
        protected override DbParameterCollection DbParameterCollection { get; } = new RecordingDbParameterCollection();
        public override void Cancel() { }
        public override int ExecuteNonQuery()
        {
            connection.LastCommandText = CommandText;
            return 0;
        }
        public override object? ExecuteScalar() => null;
        public override void Prepare() { }
        protected override DbParameter CreateDbParameter() => new RecordingDbParameter();
        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior) => throw new NotSupportedException();
    }

    private sealed class RecordingDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }
        public override ParameterDirection Direction { get; set; }
        public override bool IsNullable { get; set; }
        [AllowNull] public override string ParameterName { get; set; } = string.Empty;
        [AllowNull] public override string SourceColumn { get; set; } = string.Empty;
        public override DataRowVersion SourceVersion { get; set; }
        public override bool SourceColumnNullMapping { get; set; }
        public override object? Value { get; set; }
        public override byte Precision { get; set; }
        public override byte Scale { get; set; }
        public override int Size { get; set; }
        public override void ResetDbType() { }
    }

    private sealed class RecordingDbParameterCollection : DbParameterCollection
    {
        private readonly ArrayList _values = [];
        public override int Count => _values.Count;
        public override object SyncRoot => ((ICollection)_values).SyncRoot;
        public override int Add(object value) => _values.Add(value);
        public override void AddRange(Array values) => _values.AddRange(values);
        public override void Clear() => _values.Clear();
        public override bool Contains(object value) => _values.Contains(value);
        public override bool Contains(string value) => IndexOf(value) >= 0;
        public override void CopyTo(Array array, int index) => _values.CopyTo(array, index);
        public override IEnumerator GetEnumerator() => _values.GetEnumerator();
        public override int IndexOf(object value) => _values.IndexOf(value);
        public override int IndexOf(string parameterName) => _values.Cast<DbParameter>()
            .Select((value, index) => (value, index))
            .Where(pair => pair.value.ParameterName == parameterName)
            .Select(pair => pair.index)
            .DefaultIfEmpty(-1)
            .Single();
        public override void Insert(int index, object value) => _values.Insert(index, value);
        public override void Remove(object value) => _values.Remove(value);
        public override void RemoveAt(int index) => _values.RemoveAt(index);
        public override void RemoveAt(string parameterName)
        {
            var index = IndexOf(parameterName);
            if (index >= 0) RemoveAt(index);
        }
        protected override DbParameter GetParameter(int index) => (DbParameter)_values[index]!;
        protected override DbParameter GetParameter(string parameterName) => (DbParameter)_values[IndexOf(parameterName)]!;
        protected override void SetParameter(int index, DbParameter value) => _values[index] = value;
        protected override void SetParameter(string parameterName, DbParameter value)
        {
            var index = IndexOf(parameterName);
            if (index < 0) Add(value);
            else _values[index] = value;
        }
    }
}
