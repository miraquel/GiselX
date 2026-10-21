using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IEmailSettingRepository
{
    /// <summary>The stored configuration, or null when nobody has saved one yet.</summary>
    Task<EmailSetting?> GetAsync(CancellationToken cancellationToken);

    Task SaveAsync(EmailSetting setting, CancellationToken cancellationToken);
}
