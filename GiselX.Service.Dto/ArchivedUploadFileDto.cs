namespace GiselX.Service.Dto;

/// <summary>The archived workbook, ready to be handed back under the name it was uploaded with.</summary>
public sealed class ArchivedUploadFileDto
{
    public required Stream Content { get; init; }
    public required string FileName { get; init; }
    public string ContentType { get; init; } = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
}
