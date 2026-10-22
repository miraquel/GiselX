using GiselX.Repository.Interface;
using GiselX.Service.Interface;

namespace GiselX.Service;

public class UploadCheckService : IUploadCheckService
{
    private readonly IUploadCheckRepository _repository;

    public UploadCheckService(IUploadCheckRepository repository)
    {
        _repository = repository;
    }

    public async Task<IReadOnlyList<string>> FindMissingUploadsAsync(int companyId, DateTime fromLocal,
        DateTime toLocal, CancellationToken cancellationToken)
    {
        // The window is server-local, but SalesTransaction and Stock record UTC. Without the conversion a
        // WIB upload made before 07:00 on the first day of the window falls outside it.
        var presence = await _repository.GetUploadPresenceAsync(companyId, fromLocal.ToUniversalTime(),
            toLocal.ToUniversalTime(), cancellationToken);

        var missing = new List<string>();
        if (!presence.HasSellout) missing.Add("Sellout (Sales Transaction)");
        if (presence.MissingStockAreas.Count > 0)
            missing.AddRange(presence.MissingStockAreas.Select(area => $"Stock area {area}"));
        else if (!presence.HasStock)
            missing.Add("Stock");
        return missing;
    }
}
