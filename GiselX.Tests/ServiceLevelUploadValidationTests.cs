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
        Assert.Contains("Kolom B (SO Create Date) kosong atau bukan tanggal di 1 baris: 5.", response.Message);
        Assert.Empty(repository.Uploaded);
    }

    [Fact]
    public async Task A_blank_receipt_date_request_is_accepted()
    {
        var (service, repository, transaction) = Service();

        var response = await service.UploadAsync([Row(2, rctDateRequest: SqlDateTime.MinValue.Value)], 7, default);

        Assert.True(response.IsSuccess);
        Assert.Single(repository.Uploaded);
        Assert.True(transaction.CommitCalled);
    }

    [Fact]
    public async Task A_row_with_an_so_id_but_no_item_id_is_refused()
    {
        var (service, repository, _) = Service();

        var response = await service.UploadAsync([Row(7, itemId: "")], 7, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Kolom E (Item ID) kosong di 1 baris: 7.", response.Message);
        Assert.Empty(repository.Uploaded);
    }

    [Fact]
    public async Task A_column_failing_on_every_row_is_reported_once_with_a_hint_about_column_order()
    {
        var (service, _, _) = Service();
        var rows = Enumerable.Range(2, 225).Select(row => Row(row, soCreateDate: SqlDateTime.MinValue.Value)).ToArray();

        var response = await service.UploadAsync(rows, 7, default);

        Assert.False(response.IsSuccess);
        Assert.Contains("Kolom B (SO Create Date) kosong atau bukan tanggal di semua 225 baris data.", response.Message);
        Assert.Contains("urutan kolom berbeda dari Template", response.Message);
        Assert.Single(response.Errors);
    }

    [Fact]
    public async Task Failing_rows_are_listed_as_ranges()
    {
        var (service, _, _) = Service();
        int[] failing = [2, 3, 4, 6, 9, 10];
        var rows = Enumerable.Range(2, 10).Select(row => failing.Contains(row) ? Row(row, itemId: "") : Row(row)).ToArray();

        var response = await service.UploadAsync(rows, 7, default);

        Assert.Contains("Kolom E (Item ID) kosong di 6 baris: 2-4, 6, 9-10.", response.Message);
        Assert.DoesNotContain("urutan kolom", response.Message);
    }

    [Fact]
    public async Task The_message_stays_short_however_many_rows_fail()
    {
        var (service, _, _) = Service();
        // Every other row fails, so no two failing rows form a range.
        var rows = Enumerable.Range(2, 10_000)
            .Select(row => row % 2 == 0 ? Row(row, itemId: "", soCreateDate: SqlDateTime.MinValue.Value) : Row(row))
            .ToArray();

        var response = await service.UploadAsync(rows, 7, default);

        Assert.Contains("dan 4990 baris lainnya", response.Message);
        Assert.True(response.Message!.Length < 1_000, $"Message was {response.Message.Length} characters.");
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
        DateTime? soCreateDate = null, DateTime? rctDateRequest = null) => new()
    {
        SourceRowNumber = sourceRow,
        SoId = soId,
        ItemId = itemId,
        SoCreateDate = soCreateDate ?? new DateTime(2026, 10, 1),
        DlvDateRequest = new DateTime(2026, 10, 3),
        RctDateRequest = rctDateRequest ?? new DateTime(2026, 10, 4)
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
