namespace GiselX.Domain;

/// <summary>A GMK item already used by some company's active item mapping; there is no GMK item master table.</summary>
public sealed class GmkItemSuggestion
{
    public string GmkItemId { get; set; } = string.Empty;
    public string GmkItemName { get; set; } = string.Empty;
}
