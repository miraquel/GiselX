using GiselX.Domain;

namespace GiselX.Repository.Interface;

public interface IChannelRepository
{
    Task<IReadOnlyList<Channel>> ListAsync(bool activeOnly, CancellationToken cancellationToken);
    Task<Channel?> GetAsync(int id, CancellationToken cancellationToken);
    Task<int> InsertAsync(Channel channel, CancellationToken cancellationToken);
    Task UpdateAsync(Channel channel, CancellationToken cancellationToken);
    Task SetActiveAsync(int id, bool isActive, string modifiedBy, DateTime modifiedDate,
        CancellationToken cancellationToken);
    Task<ChannelDeleteResult> DeleteAsync(int id, CancellationToken cancellationToken);
}
