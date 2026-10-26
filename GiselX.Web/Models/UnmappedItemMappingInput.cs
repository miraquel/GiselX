namespace GiselX.Web.Models;

/// <summary>One row of the bulk form on the Unmapped tab, kept with its errors when the page is shown again.</summary>
public sealed class UnmappedItemMappingInput
{
    public string DistributorItemId { get; set; } = string.Empty;
    public string? DistributorItemName { get; set; }
    public string? GmkItemId { get; set; }
    public string? GmkItemName { get; set; }
    public List<string> Errors { get; set; } = [];

    public static string Key(string? distributorItemId) => (distributorItemId ?? string.Empty).Trim().ToUpperInvariant();
}
