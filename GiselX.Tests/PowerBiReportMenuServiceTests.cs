using System.Linq.Expressions;
using GiselX.Domain;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class PowerBiReportMenuServiceTests
{
    [Fact]
    public async Task ListMenuAsync_derives_company_internally_and_forwards_the_exact_cancellation_token()
    {
        using var cancellation = new CancellationTokenSource();
        var repository = new CapturingMenuRepository
        {
            Reports = [new PowerBiReport { Id = 3, CompanyId = 7, Name = "Sales", DisplayOrder = 2 }]
        };
        var username = "user@example.test";
        var user = new AppIdentityUser
        {
            Id = "user-1",
            UserName = username,
            NormalizedUserName = username.ToUpperInvariant(),
            CompanyId = 7
        };
        var service = new PowerBiReportMenuService(repository, new UserClaimDto { Username = username },
            CreateUserManager(user));

        var response = await service.ListMenuAsync(cancellation.Token);

        Assert.Equal(7, repository.CompanyId);
        Assert.Equal(cancellation.Token, repository.Token);
        Assert.Equal([new PowerBiReportMenuItemDto(3, "Sales")], response.Data);
    }

    private static UserManager<AppIdentityUser> CreateUserManager(AppIdentityUser user) => new(
        new SingleUserStore(user), Options.Create(new IdentityOptions()), new PasswordHasher<AppIdentityUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!,
        NullLogger<UserManager<AppIdentityUser>>.Instance);

    private sealed class CapturingMenuRepository : IPowerBiReportMenuRepository
    {
        public IReadOnlyList<PowerBiReport> Reports { get; init; } = [];
        public int? CompanyId { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<IReadOnlyList<PowerBiReport>> ListActiveByCompanyAsync(int companyId, CancellationToken token)
        {
            CompanyId = companyId;
            Token = token;
            return Task.FromResult(Reports);
        }
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
