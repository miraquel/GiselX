namespace GiselX.Service.Dto.Common;

public class PagedListRequestDto : ListRequestDto
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}