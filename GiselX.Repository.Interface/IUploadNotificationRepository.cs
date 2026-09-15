using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IUploadNotificationRepository
{
    /// <summary>
    /// Returns the company's own contact address together with the internal list configured on its
    /// Business Unit, or null when the company no longer exists.
    /// </summary>
    Task<UploadNotificationRecipients?> GetRecipientsAsync(int companyId, CancellationToken cancellationToken);
}
