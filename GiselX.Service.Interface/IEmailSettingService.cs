using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IEmailSettingService
{
    /// <summary>
    /// The stored configuration, or the appsettings values when nothing has been saved, flagged so
    /// the screen can say which one the reader is looking at.
    /// </summary>
    Task<ServiceResponse<EmailSettingDto>> GetAsync(CancellationToken cancellationToken);

    Task<ServiceResponse> SaveAsync(EmailSettingDto dto, string username, CancellationToken cancellationToken);
}
