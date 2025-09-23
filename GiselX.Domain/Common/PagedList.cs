namespace GiselX.Domain.Common;
public class PagedList<T>
{
    public PagedList(IEnumerable<T> items, int pageNumber, int pageSize, int totalCount)
    {
        Items = items;
        PageNumber = pageNumber;
        PageSize = pageSize;
        TotalCount = totalCount;
    }
    public IEnumerable<T> Items { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1 && PageNumber <= TotalPages;
    public bool HasNextPage => PageNumber < TotalPages;
    public List<string> Columns => typeof(T).GetProperties().Select(p => p.Name).ToList();
}