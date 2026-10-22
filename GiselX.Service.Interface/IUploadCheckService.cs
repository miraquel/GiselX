namespace GiselX.Service.Interface;

public interface IUploadCheckService
{
    /// <summary>
    /// What the company still has to upload for the server-local [fromLocal, toLocal) window, as the lines
    /// a reminder lists; empty when nothing is missing. Sellout and Stock are required - Service Level is
    /// not, as the go-live covers Sellout and Stock only - and a company with active Stock areas needs
    /// Stock for every one of them.
    /// </summary>
    Task<IReadOnlyList<string>> FindMissingUploadsAsync(int companyId, DateTime fromLocal, DateTime toLocal,
        CancellationToken cancellationToken);
}
