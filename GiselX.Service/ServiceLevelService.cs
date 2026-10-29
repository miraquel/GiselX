using System.Data;
using GiselX.Domain;
using GiselX.Mapper;
using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;

namespace GiselX.Service;

public class ServiceLevelService : IServiceLevelService
{
    private readonly IServiceLevelRepository _serviceLevelRepository;
    private readonly IDbTransaction _dbTransaction;
    private readonly UserClaimDto _userClaimDto;
    private readonly MapperlyMapper _mapper = new();
    private readonly UserManager<AppIdentityUser> _userManager;
    private readonly IUploadNotificationDispatcher _notifications;

    public ServiceLevelService(IServiceLevelRepository serviceLevelRepository, IDbTransaction dbTransaction, UserClaimDto userClaimDto, UserManager<AppIdentityUser> userManager, IUploadNotificationDispatcher notifications)
    {
        _serviceLevelRepository = serviceLevelRepository;
        _dbTransaction = dbTransaction;
        _userClaimDto = userClaimDto;
        _userManager = userManager;
        _notifications = notifications;
    }

    public async Task<ServiceResponse> UploadAsync(IEnumerable<ServiceLevelDto> serviceLevels, int companyId, CancellationToken cancellationToken)
    {
        var submitted = serviceLevels as ServiceLevelDto[] ?? serviceLevels.ToArray();
        if (submitted.Length == 0)
            return Refused("File tidak berisi baris data.");

        // A row with neither id is not a service-level line - a total or a note under the data - and is
        // skipped, but counted and reported rather than dropped without a word. A row with one id and not
        // the other is a real line with a gap, and is refused like any other bad cell.
        var skipped = submitted.Count(row => string.IsNullOrWhiteSpace(row.SoId) && string.IsNullOrWhiteSpace(row.ItemId));
        var transDistDtos = submitted
            .Where(row => !string.IsNullOrWhiteSpace(row.SoId) || !string.IsNullOrWhiteSpace(row.ItemId))
            .ToArray();
        if (transDistDtos.Length == 0)
            return Refused("File tidak berisi baris data.");

        // Counted before the filter so the completion mail reports how many rows the file held.
        var submittedRows = submitted.Length;

        // One line per failing column rather than per failing row: a column left blank throughout used to
        // produce one identical line for every row, a wall of text that hid the cause and was too large for
        // the TempData cookie that carries it to the page. Rows are numbered by the workbook's own row so the
        // user can find them. The reader leaves an unreadable or blank date at SqlDateTime.MinValue
        // (1753-01-01); the old check compared against DateTime.MinValue (year 1) and so never caught one.
        // Receipt Date Request is not checked: some distributors do not track it and leave the column blank,
        // so it stays at 1753-01-01, which the grid and the export both show as empty.
        var failures = RowChecks
            .Select(check => (Check: check, Rows: transDistDtos.Where(check.Fails).Select(row => row.SourceRowNumber).ToArray()))
            .Where(failure => failure.Rows.Length > 0)
            .ToArray();

        if (failures.Length > 0)
        {
            var problems = failures
                .Select(failure => transDistDtos.Length > 1 && failure.Rows.Length == transDistDtos.Length
                    ? $"Kolom {failure.Check.Column} ({failure.Check.Field}) {failure.Check.Problem} di semua {failure.Rows.Length} baris data."
                    : $"Kolom {failure.Check.Column} ({failure.Check.Field}) {failure.Check.Problem} di {failure.Rows.Length} baris: {DescribeRows(failure.Rows)}.")
                .ToList();

            var message = "File ditolak, tidak ada data yang disimpan. Perbaiki masalah berikut lalu upload ulang:\n"
                          + string.Join("\n", problems.Select(problem => $"- {problem}"));
            if (transDistDtos.Length > 1 && failures.Any(failure => failure.Rows.Length == transDistDtos.Length))
                message += "\nKolom dibaca menurut posisinya (A, B, C, ...), bukan menurut nama header. Jika satu kolom "
                           + "bermasalah di semua baris, periksa apakah kolom itu belum diisi atau urutan kolom berbeda "
                           + "dari Template yang bisa didownload di halaman ini.";

            return new ServiceResponse
            {
                Errors = problems,
                Message = message,
                StatusCode = StatusCodes.Status400BadRequest
            };
        }

        var originalTransDists = await _serviceLevelRepository.SelectByCustAsync(companyId, cancellationToken);
        
        //var existingTransDists = transDistDtos.Where(td => originalTransDists.Any(otd => otd.SoId == td.SoId && otd.ItemId == td.ItemId)).ToArray();
        var existingTransDists = originalTransDists.Where(otd => transDistDtos.Any(td => otd.SoId == td.SoId && otd.ItemId == td.ItemId)).ToArray();
        
        // update existing transaction distributions
        foreach (var existingTransDist in existingTransDists)
        {
            await _serviceLevelRepository.DeleteAsync(existingTransDist, cancellationToken);
        }

        await _serviceLevelRepository.UploadAsync(_mapper.MapToEntity(transDistDtos), cancellationToken);
        
        // Commit the transaction
        _dbTransaction.Commit();

        // Queued only after the commit: a notification for rows that rolled back would be a lie.
        // This pipeline has no upload batch, so there is no Reference to quote.
        _notifications.Enqueue(new UploadNotificationDto
        {
            CompanyId = companyId,
            Kind = UploadNotificationKind.ServiceLevel,
            TotalRows = submittedRows,
            ValidRows = transDistDtos.Length,
            InvalidRows = submittedRows - transDistDtos.Length,
            UploadedBy = _userClaimDto.Username,
            CompletedAtUtc = DateTime.UtcNow
        });

        return new ServiceResponse
        {
            Message = skipped == 0
                ? $"{transDistDtos.Length} baris Service Level berhasil diupload."
                : $"{transDistDtos.Length} baris Service Level berhasil diupload; {skipped} baris tanpa SO ID dan Item ID dilewati."
        };
    }

    private static bool IsMissing(DateTime value) => value <= System.Data.SqlTypes.SqlDateTime.MinValue.Value;

    // The controller reads cells by position, so each check names the column letter it reads, with the
    // header the download template gives that column.
    private static readonly (string Column, string Field, string Problem, Func<ServiceLevelDto, bool> Fails)[] RowChecks =
    [
        ("A", "SO ID", "kosong", row => string.IsNullOrWhiteSpace(row.SoId)),
        ("B", "SO Create Date", "kosong atau bukan tanggal", row => IsMissing(row.SoCreateDate)),
        ("E", "Item ID", "kosong", row => string.IsNullOrWhiteSpace(row.ItemId)),
        ("J", "Dlv Date Request", "kosong atau bukan tanggal", row => IsMissing(row.DlvDateRequest))
    ];

    private const int MaxRowRangesListed = 10;

    // Collapses row numbers into ranges ("2-40, 45") and stops after a few, so the message stays short
    // however many rows fail.
    private static string DescribeRows(IEnumerable<int> rowNumbers)
    {
        var ranges = new List<(int First, int Last)>();
        foreach (var row in rowNumbers.Order())
        {
            if (ranges.Count > 0 && ranges[^1].Last + 1 == row)
                ranges[^1] = (ranges[^1].First, row);
            else
                ranges.Add((row, row));
        }

        var listed = ranges.Take(MaxRowRangesListed)
            .Select(range => range.First == range.Last ? $"{range.First}" : $"{range.First}-{range.Last}");
        var unlisted = ranges.Skip(MaxRowRangesListed).Sum(range => range.Last - range.First + 1);
        return string.Join(", ", listed) + (unlisted > 0 ? $", dan {unlisted} baris lainnya" : "");
    }

    private static ServiceResponse Refused(string message) => new()
    {
        Errors = [message], Message = message, StatusCode = StatusCodes.Status400BadRequest
    };

    public async Task<ServiceResponse<PagedListDto<ServiceLevelDto>>> SelectAsync(PagedListRequestDto pagedListRequest, CancellationToken cancellationToken)
    {
        var uploader = ResolveUploaderScope();
        var pagedList = await _serviceLevelRepository.SelectAsync(_mapper.MapToEntity(pagedListRequest), uploader, cancellationToken);
        
        return new ServiceResponse<PagedListDto<ServiceLevelDto>>
        {
            Data = _mapper.MapToDto(pagedList),
            Message = "Transaction distributions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<ServiceLevelDto>>> SelectByCustPeriodAsync(string custCode, int year, int month, CancellationToken cancellationToken)
    {
        var uploader = ResolveUploaderScope();
        var serviceLevels = await _serviceLevelRepository.SelectByCustPeriodAsync(uploader, year, month, cancellationToken);
        
        return new ServiceResponse<IEnumerable<ServiceLevelDto>>
        {
            Data = _mapper.MapToDto(serviceLevels),
            Message = "Transaction distributions retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    public async Task<ServiceResponse<IEnumerable<PeriodDto>>> SelectPeriodsAsync(CancellationToken cancellationToken)
    {
        var uploader = ResolveUploaderScope();
        var periods = await _serviceLevelRepository.SelectPeriodsAsync(uploader, cancellationToken);
        
        return new ServiceResponse<IEnumerable<PeriodDto>>
        {
            Data = _mapper.MapToDto(periods),
            Message = "Periods retrieved successfully.",
            StatusCode = StatusCodes.Status200OK
        };
    }

    private string ResolveUploaderScope()
    {
        if (_userClaimDto.IsAdministrator) return string.Empty;
        if (string.IsNullOrWhiteSpace(_userClaimDto.Username))
            throw new InvalidOperationException("Authenticated username is required.");

        return _userClaimDto.Username;
    }
}
