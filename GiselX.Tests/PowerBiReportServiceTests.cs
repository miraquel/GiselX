using System.Data;
using System.Linq.Expressions;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class PowerBiReportServiceTests
{
    [Fact]
    public async Task GetViewerAsync_cannot_forward_a_company_id_from_the_caller()
    {
        var repository = new FakePowerBiReportRepository();
        var service = CreateService(repository, userCompanyId: 7);

        await service.GetViewerAsync(42, default);

        Assert.Equal((42, 7), repository.ViewerScope);
    }

    [Fact]
    public async Task GetViewerAsync_returns_not_found_without_cross_company_detail()
    {
        var service = CreateService(new FakePowerBiReportRepository(), userCompanyId: 7);

        var response = await service.GetViewerAsync(42, default);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Null(response.Data);
        Assert.All(response.Errors, error => Assert.DoesNotContain("company", error, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SaveAsync_rejects_invalid_url_without_committing()
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction, userCompanyId: 7);

        var response = await service.SaveAsync(new SavePowerBiReportDto
            { CompanyId = 7, Name = "Sales", EmbedUrl = "https://evil.example/report", DisplayOrder = 10 },
            "admin", default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("app.powerbi.com", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.ValidationErrors,
            error => error.Key == nameof(SavePowerBiReportDto.EmbedUrl));
        Assert.Equal(0, transaction.CommitCount);
    }

    [Theory]
    [InlineData(0, "Sales", "https://app.powerbi.com/view?r=abc", 10, "Company", nameof(SavePowerBiReportDto.CompanyId))]
    [InlineData(7, " ", "https://app.powerbi.com/view?r=abc", 10, "name", nameof(SavePowerBiReportDto.Name))]
    [InlineData(7, "Sales", "https://app.powerbi.com/view?r=abc", -1, "order", nameof(SavePowerBiReportDto.DisplayOrder))]
    public async Task SaveAsync_rejects_invalid_fields_without_mutation(
        int companyId, string name, string embedUrl, int displayOrder, string expectedError, string expectedKey)
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            CompanyId = companyId,
            Name = name,
            EmbedUrl = embedUrl,
            DisplayOrder = displayOrder
        }, "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.ValidationErrors, error => error.Key == expectedKey);
        Assert.Null(repository.Inserted);
        Assert.Null(repository.Updated);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_rejects_trimmed_name_over_150_characters()
    {
        var service = CreateService(new FakePowerBiReportRepository());

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            CompanyId = 7,
            Name = $"  {new string('X', 151)}  ",
            EmbedUrl = "https://app.powerbi.com/view?r=abc",
            DisplayOrder = 10
        }, "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("150", StringComparison.Ordinal));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.Name));
    }

    [Fact]
    public async Task SaveAsync_rejects_normalized_url_over_2048_characters()
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);
        var url = "https://app.powerbi.com/view?r=" + new string('a', 2049);

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            CompanyId = 7, Name = "Sales", EmbedUrl = url, DisplayOrder = 10
        }, "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("2,048", StringComparison.Ordinal));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.EmbedUrl));
        Assert.Null(repository.Inserted);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_rejects_url_that_expands_beyond_2048_characters_during_normalization()
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);
        var url = "https://app.powerbi.com/view?r=" + new string('\u00e9', 340);

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            CompanyId = 7, Name = "Sales", EmbedUrl = url, DisplayOrder = 10
        }, "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("2,048", StringComparison.Ordinal));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.EmbedUrl));
        Assert.Null(repository.Inserted);
        Assert.Null(repository.Updated);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_rejects_unknown_company_without_committing()
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var companyRepository = new FakeCompanyRepository { Companies = [new Company { Id = 8, Name = "Other" }] };
        var service = CreateService(repository, transaction, companyRepository);

        var response = await service.SaveAsync(ValidSave(), "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("company", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.CompanyId));
        Assert.Null(repository.Inserted);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_rejects_duplicate_company_name_without_committing()
    {
        var repository = new FakePowerBiReportRepository { DuplicateName = true };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction, userCompanyId: 7);

        var response = await service.SaveAsync(new SavePowerBiReportDto
            { CompanyId = 7, Name = " Sales ", EmbedUrl = "https://app.powerbi.com/view?r=abc", DisplayOrder = 10 },
            "admin", default);

        Assert.Equal(400, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.Name));
        Assert.Equal((7, "Sales", (int?)null), repository.DuplicateScope);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_normalizes_values_and_sets_utc_creation_audit()
    {
        var repository = new FakePowerBiReportRepository();
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction, userCompanyId: 7);
        var before = DateTime.UtcNow;

        var response = await service.SaveAsync(new SavePowerBiReportDto
            { CompanyId = 7, Name = "  Sales  ", EmbedUrl = " https://app.powerbi.com/view?r=abc ", DisplayOrder = 10 },
            "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal(101, response.Data);
        Assert.Equal("Sales", repository.Inserted!.Name);
        Assert.Equal("https://app.powerbi.com/view?r=abc", repository.Inserted.EmbedUrl);
        Assert.Equal("admin", repository.Inserted.CreatedBy);
        Assert.InRange(repository.Inserted.CreatedDate, before, DateTime.UtcNow);
        Assert.Null(repository.Inserted.ModifiedDate);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_update_preserves_created_audit_and_sets_modified_audit()
    {
        var created = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        var repository = new FakePowerBiReportRepository
        {
            AdminById = new PowerBiReport
                { Id = 4, CompanyId = 7, Name = "Old", EmbedUrl = "https://app.powerbi.com/view?r=old", CreatedBy = "first", CreatedDate = created }
        };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction, userCompanyId: 7);
        var before = DateTime.UtcNow;

        var response = await service.SaveAsync(new SavePowerBiReportDto
            { Id = 4, CompanyId = 7, Name = "New", EmbedUrl = "https://app.powerbi.com/view?r=new", DisplayOrder = 20 },
            "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal((7, "New", (int?)4), repository.DuplicateScope);
        Assert.Equal(created, repository.Updated!.CreatedDate);
        Assert.Equal("first", repository.Updated.CreatedBy);
        Assert.Equal("admin", repository.Updated.ModifiedBy);
        Assert.InRange(repository.Updated.ModifiedDate!.Value, before, DateTime.UtcNow);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_update_returns_not_found_when_report_does_not_exist()
    {
        var transaction = new FakeTransaction();
        var service = CreateService(new FakePowerBiReportRepository(), transaction);
        var dto = ValidSave();
        dto = new SavePowerBiReportDto
        {
            Id = 404, CompanyId = dto.CompanyId, Name = dto.Name, EmbedUrl = dto.EmbedUrl,
            DisplayOrder = dto.DisplayOrder, IsActive = dto.IsActive
        };

        var response = await service.SaveAsync(dto, "admin", default);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SaveAsync_update_returns_not_found_without_committing_when_update_affects_no_row()
    {
        var repository = new FakePowerBiReportRepository
        {
            AdminById = new PowerBiReport
            {
                Id = 4, CompanyId = 7, Name = "Old", EmbedUrl = "https://app.powerbi.com/view?r=old",
                CreatedBy = "first", CreatedDate = DateTime.UtcNow
            },
            UpdateResult = false
        };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            Id = 4, CompanyId = 7, Name = "New", EmbedUrl = "https://app.powerbi.com/view?r=new",
            DisplayOrder = 20
        }, "admin", default);

        Assert.Equal(StatusCodes.Status404NotFound, response.StatusCode);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SaveAsync_translates_concurrent_duplicate_name_conflict_without_committing(bool update)
    {
        var repository = new FakePowerBiReportRepository
        {
            AdminById = update
                ? new PowerBiReport
                {
                    Id = 4, CompanyId = 7, Name = "Old", EmbedUrl = "https://app.powerbi.com/view?r=old",
                    CreatedBy = "first", CreatedDate = DateTime.UtcNow
                }
                : null,
            MutationException = new PowerBiReportDuplicateNameException()
        };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);
        var dto = new SavePowerBiReportDto
        {
            Id = update ? 4 : null,
            CompanyId = 7,
            Name = "Sales",
            EmbedUrl = "https://app.powerbi.com/view?r=abc",
            DisplayOrder = 10
        };

        var response = await service.SaveAsync(dto, "admin", default);

        Assert.Equal(StatusCodes.Status400BadRequest, response.StatusCode);
        Assert.Contains(response.Errors, error => error.Contains("already exists", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.ValidationErrors, error => error.Key == nameof(SavePowerBiReportDto.Name));
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SetActiveAsync_returns_not_found_when_repository_affects_no_row()
    {
        var transaction = new FakeTransaction();
        var service = CreateService(new FakePowerBiReportRepository { StateChangeResult = false },
            transaction, userCompanyId: 7);

        var response = await service.SetActiveAsync(404, false, "admin", default);

        Assert.Equal(404, response.StatusCode);
        Assert.False(response.IsSuccess);
        Assert.Equal(0, transaction.CommitCount);
    }

    [Fact]
    public async Task SetActiveAsync_sets_utc_modification_audit_and_commits_once()
    {
        var repository = new FakePowerBiReportRepository { StateChangeResult = true };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);
        var before = DateTime.UtcNow;

        var response = await service.SetActiveAsync(4, false, "admin", default);

        Assert.True(response.IsSuccess);
        Assert.Equal((4, false, "admin"), repository.StateChange!.Value.Core);
        Assert.InRange(repository.StateChange.Value.ModifiedDate, before, DateTime.UtcNow);
        Assert.Equal(1, transaction.CommitCount);
    }

    [Theory]
    [InlineData(false, 404, 0)]
    [InlineData(true, 200, 1)]
    public async Task DeleteAsync_commits_only_when_a_report_is_deleted(bool deleteResult, int statusCode,
        int expectedCommitCount)
    {
        var repository = new FakePowerBiReportRepository { DeleteResult = deleteResult };
        var transaction = new FakeTransaction();
        var service = CreateService(repository, transaction);

        var response = await service.DeleteAsync(9, default);

        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(expectedCommitCount, transaction.CommitCount);
    }

    [Fact]
    public async Task Admin_reads_map_company_names_and_return_not_found_for_missing_id()
    {
        var repository = new FakePowerBiReportRepository
        {
            Admin = [new PowerBiReport { Id = 4, CompanyId = 7, Name = "Sales", EmbedUrl = "https://app.powerbi.com/view?r=abc" }]
        };
        var companies = new FakeCompanyRepository { Companies = [new Company { Id = 7, Name = "Distributor" }] };
        var service = CreateService(repository, companyRepository: companies);

        var list = await service.ListAdminAsync(null, default);
        var missing = await service.GetAdminAsync(404, default);

        Assert.Equal("Distributor", Assert.Single(list.Data!).CompanyName);
        Assert.Equal(StatusCodes.Status404NotFound, missing.StatusCode);
        Assert.Null(missing.Data);
    }

    [Fact]
    public async Task ListCompanyOptionsAsync_returns_company_options_in_repository_order()
    {
        var service = CreateService(new FakePowerBiReportRepository(), companyRepository: new FakeCompanyRepository
        {
            Companies = [new Company { Id = 7, Name = "Alpha" }, new Company { Id = 8, Name = "Beta" }]
        });

        var response = await service.ListCompanyOptionsAsync(default);

        Assert.Equal([new CompanyOptionDto(7, "Alpha"), new CompanyOptionDto(8, "Beta")], response.Data);
    }

    [Fact]
    public async Task GetNextDisplayOrderAsync_returns_repository_value_for_existing_company()
    {
        var repository = new FakePowerBiReportRepository { NextDisplayOrder = 12 };
        var service = CreateService(repository);

        var response = await service.GetNextDisplayOrderAsync(7, default);

        Assert.True(response.IsSuccess);
        Assert.Equal(12, response.Data);
        Assert.Equal(7, repository.NextOrderCompanyId);
    }

    private static SavePowerBiReportDto ValidSave() => new()
    {
        CompanyId = 7,
        Name = "Sales",
        EmbedUrl = "https://app.powerbi.com/view?r=abc",
        DisplayOrder = 10
    };

    private static PowerBiReportService CreateService(
        FakePowerBiReportRepository repository,
        FakeTransaction? transaction = null,
        FakeCompanyRepository? companyRepository = null,
        int userCompanyId = 7)
    {
        var username = "user@example.test";
        var user = new AppIdentityUser
        {
            Id = "user-1",
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            CompanyId = userCompanyId
        };
        return new PowerBiReportService(repository, companyRepository ?? new FakeCompanyRepository(),
            transaction ?? new FakeTransaction(), new UserClaimDto { Username = username }, CreateUserManager(user));
    }

    private static UserManager<AppIdentityUser> CreateUserManager(AppIdentityUser user) => new(
        new SingleUserStore(user), Options.Create(new IdentityOptions()), new PasswordHasher<AppIdentityUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppIdentityUser>>.Instance);

    private sealed class FakePowerBiReportRepository : IPowerBiReportRepository
    {
        public IReadOnlyList<PowerBiReport> Admin { get; init; } = [];
        public IReadOnlyList<PowerBiReport> Active { get; init; } = [];
        public PowerBiReport? AdminById { get; init; }
        public bool DuplicateName { get; init; }
        public int NextDisplayOrder { get; init; } = 1;
        public bool UpdateResult { get; init; } = true;
        public bool StateChangeResult { get; init; }
        public bool DeleteResult { get; init; }
        public Exception? MutationException { get; init; }
        public int? ListedCompanyId { get; private set; }
        public (int Id, int CompanyId)? ViewerScope { get; private set; }
        public (int CompanyId, string Name, int? ExcludeId)? DuplicateScope { get; private set; }
        public int? NextOrderCompanyId { get; private set; }
        public PowerBiReport? Inserted { get; private set; }
        public PowerBiReport? Updated { get; private set; }
        public (ValueTuple<int, bool, string> Core, DateTime ModifiedDate)? StateChange { get; private set; }

        public Task<IReadOnlyList<PowerBiReport>> ListAdminAsync(int? companyId, CancellationToken token) =>
            Task.FromResult(Admin);

        public Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token)
        {
            ListedCompanyId = companyId;
            return Task.FromResult(Active);
        }

        public Task<PowerBiReport?> GetAdminByIdAsync(int id, CancellationToken token) =>
            Task.FromResult(AdminById?.Id == id ? AdminById : null);

        public Task<PowerBiReport?> GetActiveByIdAndCompanyAsync(int id, int companyId, CancellationToken token)
        {
            ViewerScope = (id, companyId);
            return Task.FromResult(Active.SingleOrDefault(report => report.Id == id && report.CompanyId == companyId));
        }

        public Task<bool> NameExistsAsync(int companyId, string name, int? excludeId, CancellationToken token)
        {
            DuplicateScope = (companyId, name, excludeId);
            return Task.FromResult(DuplicateName);
        }

        public Task<int> GetNextDisplayOrderAsync(int companyId, CancellationToken token)
        {
            NextOrderCompanyId = companyId;
            return Task.FromResult(NextDisplayOrder);
        }

        public Task<int> InsertAsync(PowerBiReport report, CancellationToken token)
        {
            if (MutationException is not null) throw MutationException;
            Inserted = report;
            return Task.FromResult(101);
        }

        public Task<bool> UpdateAsync(PowerBiReport report, CancellationToken token)
        {
            if (MutationException is not null) throw MutationException;
            Updated = report;
            return Task.FromResult(UpdateResult);
        }

        public Task<bool> SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
            CancellationToken token)
        {
            StateChange = ((id, isActive, modifiedBy), modifiedDate);
            return Task.FromResult(StateChangeResult);
        }

        public Task<bool> DeleteAsync(int id, CancellationToken token) => Task.FromResult(DeleteResult);
    }

    private sealed class FakeCompanyRepository : ICompanyRepository
    {
        public IReadOnlyList<Company> Companies { get; init; } = [new Company { Id = 7, Name = "Distributor" }];

        public Task<IReadOnlyList<Company>> GetAllAsync(CancellationToken cancellationToken) =>
            Task.FromResult(Companies);

        public Task<Company> CreateCompanyAsync(Company company, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<Company?> GetCompanyByIdAsync(int id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<PagedList<Company>> GetCompaniesAsync(PagedListRequest pagedListRequest,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<Company> UpdateCompanyAsync(Company company, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<IEnumerable<Company>> GetAllWithContactEmailAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class FakeTransaction : IDbTransaction
    {
        public int CommitCount { get; private set; }
        public IDbConnection? Connection => null;
        public IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;
        public void Commit() => CommitCount++;
        public void Dispose() { }
        public void Rollback() { }
    }

    private sealed class SingleUserStore(AppIdentityUser user)
        : IUserStore<AppIdentityUser>, IQueryableUserStore<AppIdentityUser>
    {
        public IQueryable<AppIdentityUser> Users => new TestAsyncEnumerable<AppIdentityUser>([user]);
        public void Dispose() { }
        public Task<string> GetUserIdAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.Id);
        public Task<string?> GetUserNameAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.UserName);
        public Task SetUserNameAsync(AppIdentityUser value, string? userName, CancellationToken cancellationToken)
        { value.UserName = userName; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(value.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(AppIdentityUser value, string? normalizedName,
            CancellationToken cancellationToken)
        { value.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppIdentityUser value, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);
        public Task<AppIdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<AppIdentityUser?>(user.Id == userId ? user : null);
        public Task<AppIdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<AppIdentityUser?>(string.Equals(user.NormalizedUserName, normalizedUserName,
                StringComparison.OrdinalIgnoreCase) ? user : null);
    }

    private sealed class TestAsyncEnumerable<T>(IEnumerable<T> values)
        : EnumerableQuery<T>(values), IAsyncEnumerable<T>, IQueryable<T>
    {
        public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
            new TestAsyncEnumerator<T>(((IEnumerable<T>)this).GetEnumerator());

        IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
    }

    private sealed class TestAsyncEnumerator<T>(IEnumerator<T> inner) : IAsyncEnumerator<T>
    {
        public T Current => inner.Current;
        public ValueTask DisposeAsync() { inner.Dispose(); return ValueTask.CompletedTask; }
        public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(inner.MoveNext());
    }

    private sealed class TestAsyncQueryProvider<T>(IQueryProvider inner) : IAsyncQueryProvider
    {
        public IQueryable CreateQuery(Expression expression) =>
            new TestAsyncEnumerable<T>(inner.CreateQuery<T>(expression));
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(inner.CreateQuery<TElement>(expression));
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments().Single();
            var executionResult = typeof(IQueryProvider).GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType).Invoke(inner, [expression]);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [executionResult])!;
        }
    }
}
