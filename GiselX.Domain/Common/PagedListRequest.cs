namespace GiselX.Domain.Common;
public class PagedListRequest : ListRequest
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    
}