using System.Data.SqlTypes;
using GiselX.Domain;
using GiselX.Domain.Common;
using GiselX.Repository.Interface;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Tests;

/// <summary>
/// The legacy Service Level upload stored bad data without saying so: unreadable dates became 1753-01-01
/// because the check looked for DateTime.MinValue, rows missing an SO or Item ID vanished, row numbers in
/// errors counted the rows that survived rather than the workbook's rows, and an empty sheet threw.
/// </summary>
public sealed class ServiceLevelUploadValidationTests
{
    [Fact]
    public async Task A_missing_or_unreadable_date_is_refused_with_the_workbook_row_number()
    {
        var (service, repository, _) = Service();

        var response = await service.UploadAsync([Row(5, soCreateDate: SqlDateTime.MinValue.Value)], 7, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Baris 5: SO Create Date kosong atau bukan tanggal.", response.Message);
        Assert.Empty(repository.Uploaded);
    }

    [Fact]
    public async Task A_row_with_an_so_id_but_no_item_id_is_refused()
    {
        var (service, repository, _) = Service();

        var response = await service.UploadAsync([Row(7, itemId: "")], 7, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Baris 7: Item ID kosong.", response.Message);
        Assert.Empty(repository.Uploaded);
    }

    [Fact]
    public async Task A_row_with_neither_id_is_skipped_and_reported()
    {
        var (service, repository, transaction) = Service();

        var response = await service.UploadAsync([Row(2), Row(9, soId: "", itemId: "")], 7, default);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Uploaded);
        Assert.Contains("1 baris tanpa SO ID dan Item ID dilewati", response.Message);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task A_workbook_without_data_rows_is_refused_rather_than_throwing()
    {
        var (service, repository, _) = Service();

        var response = await service.UploadAsync([], 7, default);

        Assert.False(response.IsSuccess);
        Assert.Equal("File tidak berisi baris data.", response.Message);
        Assert.Empty(repository.Uploaded);
    }

    private static ServiceLevelDto Row(int sourceRow, string soId = "SO-1", string itemId = "IT-1",
        DateTime? soCreateDate = null) => new()
    {
        SourceRowNumber = sourceRow,
        SoId = soId,
        ItemId = itemId,
        SoCreateDate = soCreateDate ?? new DateTime(2026, 10, 1),
        DlvDateRequest = new DateTime(2026, 10, 3),
        RctDateRequest = new DateTime(2026, 10, 4)
    };

    private static (ServiceLevelService, FakeRepository, CompanyServiceFakes.RecordingTransaction) Service()
    {
        var repository = new FakeRepository();
        var transaction = new CompanyServiceFakes.RecordingTransaction();
        return (new ServiceLevelService(repository, transaction, new UserClaimDto { Username = "dist" }, null!,
            new RecordingUploadNotificationDispatcher()), repository, transaction);
    }

    private sealed class FakeRepository : IServiceLevelRepository
    {
        public List<ServiceLevel> Uploaded { get; } = [];

        public Task UploadAsync(IEnumerable<ServiceLevel> values, CancellationToken token)
        {
            Uploaded.AddRange(values);
            return Task.CompletedTask;
        }
        public Task<IEnumerable<ServiceLevel>> SelectByCustAsync(int id, CancellationToken token) =>
            Task.FromResult<IEnumerable<ServiceLevel>>([]);
        public Task DeleteAsync(ServiceLevel value, CancellationToken token) => Task.CompletedTask;
        public Task<PagedList<ServiceLevel>> SelectAsync(PagedListRequest request, string uploader, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<IEnumerable<ServiceLevel>> SelectByCustPeriodAsync(string uploader, int year, int month, CancellationToken token) =>
            throw new NotSupportedException();
        public Task<IEnumerable<Period>> SelectPeriodsAsync(string uploader, CancellationToken token) =>
            throw new NotSupportedException();
    }
}
