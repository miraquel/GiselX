namespace GiselX.Service.Dto;

public sealed class PowerBiReportDto
{
    public int Id { get; init; }
    public int CompanyId { get; init; }
    public string CompanyName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; }
    public DateTime CreatedDate { get; init; }
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? ModifiedDate { get; init; }
    public string? ModifiedBy { get; init; }
}

public sealed class SavePowerBiReportDto
{
    public int? Id { get; init; }
    public int CompanyId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string EmbedUrl { get; init; } = string.Empty;
    public int DisplayOrder { get; init; }
    public bool IsActive { get; init; } = true;
}

public sealed record PowerBiReportMenuItemDto(int Id, string Name);
public sealed record PowerBiReportViewerDto(int Id, string Name, string EmbedUrl);
public sealed record CompanyOptionDto(int Id, string Name);
