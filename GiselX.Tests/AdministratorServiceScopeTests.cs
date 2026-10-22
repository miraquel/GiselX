using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace GiselX.Tests;

public sealed class AdministratorServiceScopeTests
{
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 7)]
    public async Task Stock_service_uses_cross_company_scope_only_for_administrator(bool isAdministrator,
        int expectedCompanyId)
    {
        var repository = new CapturingStockRepository();
        var service = new StockService(repository, null!, Claims(isAdministrator), UserManager());

        await service.SelectAsync(new PagedListRequestDto(), false, CancellationToken.None);

        Assert.Equal(expectedCompanyId, repository.SelectedCompanyId);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 7)]
    public async Task Sellout_service_uses_cross_company_scope_only_for_administrator(bool isAdministrator,
        int expectedCompanyId)
    {
        var repository = new CapturingSalesRepository();
        var service = new SalesTransactionService(repository, null!, Claims(isAdministrator), UserManager());

        await service.SelectAsync(new PagedListRequestDto(), false, CancellationToken.None);

        Assert.Equal(expectedCompanyId, repository.SelectedCompanyId);
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 7)]
    public async Task Stock_filtered_summary_uses_cross_company_scope_only_for_administrator(bool isAdministrator,
        int expectedCompanyId)
    {
        var repository = new CapturingStockRepository();
        var service = new StockService(repository, null!, Claims(isAdministrator), UserManager());

        var response = await service.SelectSummaryAsync(new PagedListRequestDto(), false, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expectedCompanyId, repository.SummaryCompanyId);
        Assert.Equal((2, 5, 90m), (response.Data!.DistributorCount, response.Data.RowCount, response.Data.QtyPcs));
    }

    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 7)]
    public async Task Sellout_filtered_summary_uses_cross_company_scope_only_for_administrator(bool isAdministrator,
        int expectedCompanyId)
    {
        var repository = new CapturingSalesRepository();
        var service = new SalesTransactionService(repository, null!, Claims(isAdministrator), UserManager());

        var response = await service.SelectSummaryAsync(new PagedListRequestDto(), false, CancellationToken.None);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(expectedCompanyId, repository.SummaryCompanyId);
        Assert.Equal((3, 8, 120m), (response.Data!.DistributorCount, response.Data.RowCount, response.Data.NetValue));
    }

    [Theory]
    [InlineData(true, "")]
    [InlineData(false, "distributor")]
    public async Task Service_level_service_uses_cross_uploader_scope_only_for_administrator(bool isAdministrator,
        string expectedUploader)
    {
        var repository = new CapturingServiceLevelRepository();
        var service = new ServiceLevelService(repository, null!, Claims(isAdministrator), UserManager(),
            new RecordingUploadNotificationDispatcher());

        await service.SelectAsync(new PagedListRequestDto(), CancellationToken.None);

        Assert.Equal(expectedUploader, repository.SelectedUploader);
    }

    [Theory]
    [InlineData(true, 99, 0)]
    [InlineData(false, 0, 7)]
    public async Task Stock_period_scope_ignores_caller_supplied_company_id(bool isAdministrator,
        int suppliedCompanyId, int expectedCompanyId)
    {
        var repository = new CapturingStockRepository();
        var service = new StockService(repository, null!, Claims(isAdministrator), UserManager());

        await service.SelectByCustPeriodAsync(suppliedCompanyId, 2026, 8, CancellationToken.None);

        Assert.Equal(expectedCompanyId, repository.PeriodCompanyId);
    }

    [Theory]
    [InlineData(true, 99, 0)]
    [InlineData(false, 0, 7)]
    public async Task Sellout_period_scope_ignores_caller_supplied_company_id(bool isAdministrator,
        int suppliedCompanyId, int expectedCompanyId)
    {
        var repository = new CapturingSalesRepository();
        var service = new SalesTransactionService(repository, null!, Claims(isAdministrator), UserManager());

        await service.SelectByCustPeriodAsync(suppliedCompanyId, 2026, 8, false, CancellationToken.None);

        Assert.Equal(expectedCompanyId, repository.PeriodCompanyId);
    }

    [Theory]
    [InlineData(true, "forged-distributor", "")]
    [InlineData(false, "", "distributor")]
    public async Task Service_level_period_scope_ignores_caller_supplied_uploader(bool isAdministrator,
        string suppliedUploader, string expectedUploader)
    {
        var repository = new CapturingServiceLevelRepository();
        var service = new ServiceLevelService(repository, null!, Claims(isAdministrator), UserManager(),
            new RecordingUploadNotificationDispatcher());

        await service.SelectByCustPeriodAsync(suppliedUploader, 2026, 8, CancellationToken.None);

        Assert.Equal(expectedUploader, repository.PeriodUploader);
    }

    private static UserClaimDto Claims(bool administrator) => new()
    {
        Username = "distributor", IsAdministrator = administrator
    };

    private static UserManager<AppIdentityUser> UserManager()
    {
        var user = new AppIdentityUser
        {
            Id = "user-1", UserName = "distributor", NormalizedUserName = "DISTRIBUTOR", CompanyId = 7
        };
        return new UserManager<AppIdentityUser>(new SingleUserStore(user), Options.Create(new IdentityOptions()),
            new PasswordHasher<AppIdentityUser>(), [], [], new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(), null!, NullLogger<UserManager<AppIdentityUser>>.Instance);
    }

    private sealed class SingleUserStore(AppIdentityUser user) : IUserStore<AppIdentityUser>
    {
        public void Dispose() { }
        public Task<string> GetUserIdAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(value.Id);
        public Task<string?> GetUserNameAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(value.UserName);
        public Task SetUserNameAsync(AppIdentityUser value, string? name, CancellationToken token) { value.UserName = name; return Task.CompletedTask; }
        public Task<string?> GetNormalizedUserNameAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(value.NormalizedUserName);
        public Task SetNormalizedUserNameAsync(AppIdentityUser value, string? name, CancellationToken token) { value.NormalizedUserName = name; return Task.CompletedTask; }
        public Task<IdentityResult> CreateAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> UpdateAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(AppIdentityUser value, CancellationToken token) => Task.FromResult(IdentityResult.Success);
        public Task<AppIdentityUser?> FindByIdAsync(string id, CancellationToken token) => Task.FromResult<AppIdentityUser?>(user.Id == id ? user : null);
        public Task<AppIdentityUser?> FindByNameAsync(string name, CancellationToken token) =>
            Task.FromResult<AppIdentityUser?>(user.NormalizedUserName == name ? user : null);
    }

    private sealed class CapturingStockRepository : IStockRepository
    {
        public int? SelectedCompanyId { get; private set; }
        public int? PeriodCompanyId { get; private set; }
        public int? SummaryCompanyId { get; private set; }
        public Task<PagedList<Stock>> SelectAsync(PagedListRequest request, int companyId, bool identity, CancellationToken token)
        { SelectedCompanyId = companyId; return Task.FromResult(new PagedList<Stock>([], 1, 10, 0)); }
        public Task<StockSelectionSummary> SummarizeAsync(PagedListRequest request, int companyId, bool identity, CancellationToken token)
        {
            SummaryCompanyId = companyId;
            return Task.FromResult(new StockSelectionSummary { DistributorCount = 2, RowCount = 5, QtyPcs = 90m });
        }
        public Task UploadAsync(IEnumerable<Stock> values, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<Stock>> SelectByCustAsync(int id, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<Stock>> SelectByCustPeriodAsync(int id, int year, int month, CancellationToken token)
        { PeriodCompanyId = id; return Task.FromResult<IEnumerable<Stock>>([]); }
        public Task<IEnumerable<Period>> SelectPeriodsAsync(int id, CancellationToken token) => throw new NotSupportedException();
        public Task<IReadOnlyList<StockUploadBatch>> ListCompletedUploadsAsync(int id, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<Stock>> SelectByBatchAsync(long batch, int id, bool identity, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class CapturingSalesRepository : ISalesTransactionRepository
    {
        public int? SelectedCompanyId { get; private set; }
        public int? PeriodCompanyId { get; private set; }
        public int? SummaryCompanyId { get; private set; }
        public Task<PagedList<SalesTransaction>> SelectAsync(PagedListRequest request, int companyId, bool identity, CancellationToken token)
        { SelectedCompanyId = companyId; return Task.FromResult(new PagedList<SalesTransaction>([], 1, 10, 0)); }
        public Task<SalesSelectionSummary> SummarizeAsync(PagedListRequest request, int companyId, bool identity, CancellationToken token)
        {
            SummaryCompanyId = companyId;
            return Task.FromResult(new SalesSelectionSummary { DistributorCount = 3, RowCount = 8, NetValue = 120m });
        }
        public Task UploadAsync(IEnumerable<SalesTransaction> values, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<SalesTransaction>> SelectByCustAsync(int id, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<SalesTransaction>> SelectByCustPeriodAsync(int id, int year, int month, bool identity, CancellationToken token)
        { PeriodCompanyId = id; return Task.FromResult<IEnumerable<SalesTransaction>>([]); }
        public Task DeleteAsync(SalesTransaction value, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<Period>> SelectPeriodsAsync(int id, CancellationToken token) => throw new NotSupportedException();
    }

    private sealed class CapturingServiceLevelRepository : IServiceLevelRepository
    {
        public string? SelectedUploader { get; private set; }
        public string? PeriodUploader { get; private set; }
        public Task<PagedList<ServiceLevel>> SelectAsync(PagedListRequest request, string uploader, CancellationToken token)
        { SelectedUploader = uploader; return Task.FromResult(new PagedList<ServiceLevel>([], 1, 10, 0)); }
        public Task UploadAsync(IEnumerable<ServiceLevel> values, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<ServiceLevel>> SelectByCustAsync(int id, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<ServiceLevel>> SelectByCustPeriodAsync(string uploader, int year, int month, CancellationToken token)
        { PeriodUploader = uploader; return Task.FromResult<IEnumerable<ServiceLevel>>([]); }
        public Task DeleteAsync(ServiceLevel value, CancellationToken token) => throw new NotSupportedException();
        public Task<IEnumerable<Period>> SelectPeriodsAsync(string uploader, CancellationToken token) => throw new NotSupportedException();
    }
}
