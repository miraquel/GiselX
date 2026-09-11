using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;

namespace GiselX.Service.Interface;

public interface IChannelService
{
    Task<ServiceResponse<IReadOnlyList<ChannelDto>>> ListAsync(bool activeOnly, CancellationToken token);
    Task<ServiceResponse<string>> GetNextCodeAsync(CancellationToken token);
    Task<ServiceResponse<ChannelDto?>> GetAsync(int id, CancellationToken token);
    Task<ServiceResponse<int>> SaveAsync(ChannelDto dto, string username, CancellationToken token);
    Task<ServiceResponse> SetActiveAsync(int id, bool active, string username, CancellationToken token);
    Task<ServiceResponse> DeleteAsync(int id, CancellationToken token);
}
