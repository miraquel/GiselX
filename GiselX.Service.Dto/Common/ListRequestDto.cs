namespace GiselX.Service.Dto.Common;

public class ListRequestDto
{
    public string SortBy { get; set; } = string.Empty;
    public bool IsSortAscending { get; set; }
    public string? SearchTerm { get; set; }
    public Dictionary<string, string> Filters { get; set; } = new();
}
