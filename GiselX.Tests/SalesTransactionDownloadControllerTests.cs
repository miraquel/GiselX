using ClosedXML.Excel;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Controllers.Api;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Security.Claims;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Query;

namespace GiselX.Tests;

public sealed class SalesTransactionDownloadControllerTests
{
    [Theory]
    [InlineData("Distributor", "P-DIST", "Distributor Product", true)]
    [InlineData("GMK Administrator", "4100039", "GMK Product", false)]
    public async Task Download_writes_identity_for_the_callers_exact_role(string role, string productId,
        string productName, bool expectedDistributorMode)
    {
        var service = new RoleAwareSalesServiceFake();
        var controller = CreateMvcController(service, role);

        var result = Assert.IsType<FileStreamResult>(await controller.DownloadSelectByCustPeriod(2026, 8,
            CancellationToken.None));
        using var workbook = new XLWorkbook(result.FileStream);
        var sheet = workbook.Worksheet(1);

        Assert.Equal(expectedDistributorMode, service.UseDistributorIdentity);
        Assert.Equal(productId, sheet.Cell(2, 5).GetString());
        Assert.Equal(productName, sheet.Cell(2, 7).GetString());
    }

    [Theory]
    [InlineData("Distributor", true)]
    [InlineData("GMK Administrator", false)]
    public async Task Api_select_uses_distributor_mode_only_for_the_exact_role(string role, bool expectedDistributorMode)
    {
        var service = new RoleAwareSalesServiceFake();
        var controller = new GiselX.Web.Controllers.Api.SalesTransactionsController(service)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = Principal(role)
                }
            }
        };

        await controller.SelectAsync(new PagedListRequestDto(), CancellationToken.None);

        Assert.Equal(expectedDistributorMode, service.UseDistributorIdentity);
    }

    private static GiselX.Web.Controllers.SalesTransactionsController CreateMvcController(
        RoleAwareSalesServiceFake service, string role)
    {
        var user = new AppIdentityUser
        {
            Id = "user-1", UserName = "user@example.test", CompanyId = 7,
            Company = new Company { Id = 7, Name = "Test Distributor" }
        };
        return new GiselX.Web.Controllers.SalesTransactionsController(service, null!, CreateUserManager(user))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = Principal(role, user.Id) }
            }
        };
    }

    private static ClaimsPrincipal Principal(string role, string userId = "user-1") => new(
        new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, userId), new Claim(ClaimTypes.Name, "user@example.test"),
            new Claim(ClaimTypes.Role, role)], "test",
            ClaimTypes.Name, ClaimTypes.Role));

    private static UserManager<AppIdentityUser> CreateUserManager(AppIdentityUser user) => new(
        new SingleUserStore(user), Options.Create(new IdentityOptions()), new PasswordHasher<AppIdentityUser>(), [], [],
        new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppIdentityUser>>.Instance);

    private sealed class RoleAwareSalesServiceFake : ISalesTransactionService
    {
        public bool UseDistributorIdentity { get; private set; }
        public Task<ServiceResponse<PagedListDto<SalesTransactionDto>>> SelectAsync(PagedListRequestDto request,
            bool useDistributorIdentity, CancellationToken cancellationToken)
        {
            UseDistributorIdentity = useDistributorIdentity;
            return Task.FromResult(new ServiceResponse<PagedListDto<SalesTransactionDto>>());
        }

        public Task<ServiceResponse<IEnumerable<SalesTransactionDto>>> SelectByCustPeriodAsync(int companyId, int year,
            int month, bool useDistributorIdentity, CancellationToken cancellationToken)
        {
            UseDistributorIdentity = useDistributorIdentity;
            return Task.FromResult(new ServiceResponse<IEnumerable<SalesTransactionDto>>
            {
                Data =
                [
                    new SalesTransactionDto
                    {
                        CustomerId = "C-1", CustomerName = "Customer", ProductId = useDistributorIdentity ? "P-DIST" : "4100039",
                        ProductName = useDistributorIdentity ? "Distributor Product" : "GMK Product", InvoiceNo = "I-1",
                        InvoiceDate = new DateTime(2026, 8, 1), CompanyId = companyId
                    }
                ]
            });
        }

        public Task<ServiceResponse> UploadAsync(IEnumerable<SalesTransactionDto> salesTransactions, int companyId,
            CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class SingleUserStore(AppIdentityUser user) : IUserStore<AppIdentityUser>, IQueryableUserStore<AppIdentityUser>
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
        public Task SetNormalizedUserNameAsync(AppIdentityUser value, string? normalizedName, CancellationToken cancellationToken)
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

    private sealed class TestAsyncEnumerable<T>(IEnumerable<T> values) : EnumerableQuery<T>(values), IAsyncEnumerable<T>, IQueryable<T>
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
        public IQueryable CreateQuery(Expression expression) => new TestAsyncEnumerable<T>(inner.CreateQuery<T>(expression));
        public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
            new TestAsyncEnumerable<TElement>(inner.CreateQuery<TElement>(expression));
        public object? Execute(Expression expression) => inner.Execute(expression);
        public TResult Execute<TResult>(Expression expression) => inner.Execute<TResult>(expression);
        public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
        {
            var resultType = typeof(TResult).GetGenericArguments().Single();
            var executionResult = typeof(IQueryProvider).GetMethods()
                .Single(method => method.Name == nameof(IQueryProvider.Execute) && method.IsGenericMethod)
                .MakeGenericMethod(resultType)
                .Invoke(inner, [expression]);
            return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
                .MakeGenericMethod(resultType).Invoke(null, [executionResult])!;
        }
    }
}
