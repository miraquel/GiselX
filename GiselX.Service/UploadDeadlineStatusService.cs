using GiselX.Repository.Interface;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class UploadDeadlineStatusService(
    ICompanyRepository companies,
    IUploadCheckService uploadCheck,
    TimeProvider? clock = null) : IUploadDeadlineStatusService
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System;

    public async Task<UploadDeadlineStatusDto?> GetAsync(int companyId, CancellationToken cancellationToken)
    {
        var company = await companies.GetCompanyByIdAsync(companyId, cancellationToken);
        if (company is null) return null;

        var today = _clock.GetLocalNow().Date;
        if (ReminderCycle.NextDeadline(company, today) is not { } deadline) return null;

        var (from, to) = ReminderCycle.WindowFor(company, deadline);
        return new UploadDeadlineStatusDto
        {
            Deadline = deadline,
            From = from,
            DaysLeft = (deadline - today).Days,
            Missing = await uploadCheck.FindMissingUploadsAsync(companyId, from, to, cancellationToken)
        };
    }
}
