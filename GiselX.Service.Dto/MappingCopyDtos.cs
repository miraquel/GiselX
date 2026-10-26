namespace GiselX.Service.Dto;

public enum TemplateCopyOutcome
{
    WillCopy = 0,
    SkippedNameExists = 1,
    SkippedFormatExists = 2,
    Invalid = 3
}

public sealed class CopySourceCompanyDto
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
}

public sealed class TemplateCopyCandidateDto
{
    public int SourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string WorksheetName { get; set; } = string.Empty;
    public int HeaderRowNumber { get; set; }
    public int FirstDataRowNumber { get; set; }
    public int ColumnCount { get; set; }
    public bool IsActive { get; set; }
    public TemplateCopyOutcome Outcome { get; set; }
    public List<string> Reasons { get; set; } = [];
}

public sealed class MappingCopyPreviewDto
{
    public int SourceCompanyId { get; set; }
    public int TargetCompanyId { get; set; }
    public List<TemplateCopyCandidateDto> SelloutTemplates { get; set; } = [];
    public List<TemplateCopyCandidateDto> StockTemplates { get; set; } = [];
    public int ItemMappingsToCopy { get; set; }
    public int ItemMappingsSkipped { get; set; }
    public bool TargetItemMappingEnabled { get; set; }
}

public sealed class MappingCopyRequestDto
{
    public int SourceCompanyId { get; set; }
    public int TargetCompanyId { get; set; }
    public List<int> SelloutMappingIds { get; set; } = [];
    public List<int> StockMappingIds { get; set; } = [];
    public bool CopyItemMappings { get; set; }
}

public sealed class MappingCopyResultDto
{
    public int SelloutCopied { get; set; }
    public int StockCopied { get; set; }
    public int ItemMappingsCopied { get; set; }
    public int ItemMappingsSkipped { get; set; }

    /// <summary>One line per template that was not copied, e.g. "Sellout 'Weekly': reason".</summary>
    public List<string> Skipped { get; set; } = [];
}
