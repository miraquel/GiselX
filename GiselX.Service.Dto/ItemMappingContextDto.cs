namespace GiselX.Service.Dto;

/// <summary>
/// The active item mappings for one company, together with the lookup both upload pipelines resolve every
/// parsed row through. The index is built once on first use rather than rescanned per row: a workbook of
/// 50,000 rows against 3,000 mappings costs 150 million string comparisons when each row walks the list,
/// and the comparison trims both sides on every step.
/// </summary>
public sealed class ItemMappingContextDto
{
    private Dictionary<string, ItemMappingDto>? _activeByDistributorItemId;
    private ItemMappingDto? _activeWithoutDistributorItemId;

    public bool IsEnabled { get; init; }
    public IReadOnlyList<ItemMappingDto> Mappings { get; init; } = [];

    /// <summary>
    /// The active mapping claiming a distributor item id, matched case-insensitively on trimmed text, or
    /// null when none applies. Where several active mappings claim one id the earliest in
    /// <see cref="Mappings"/> wins, which is the order a linear first-match scan produced.
    /// </summary>
    public ItemMappingDto? FindActive(string? distributorItemId)
    {
        EnsureIndexed();
        var key = distributorItemId?.Trim();
        return key is null
            ? _activeWithoutDistributorItemId
            : _activeByDistributorItemId!.GetValueOrDefault(key);
    }

    private void EnsureIndexed()
    {
        if (_activeByDistributorItemId is not null) return;

        var index = new Dictionary<string, ItemMappingDto>(Mappings.Count, StringComparer.OrdinalIgnoreCase);
        foreach (var mapping in Mappings)
        {
            if (!mapping.IsActive) continue;

            var key = mapping.DistributorItemId?.Trim();
            if (key is null) _activeWithoutDistributorItemId ??= mapping;
            else index.TryAdd(key, mapping);
        }

        _activeByDistributorItemId = index;
    }
}
