using GiselX.Domain;

namespace GiselX.Service.Dto;

public sealed class StockStagingErrorDto
{
    public int SourceRowNumber { get; set; }

    /// <summary>
    /// Worksheet the rejected row was read from. A workbook can contribute rows from several mapped
    /// worksheets, so the row number alone does not identify a row.
    /// </summary>
    public string? WorksheetName { get; set; }

    public StockCanonicalField? Field { get; set; }
    public string? RejectedValue { get; set; }
    public string Reason { get; set; } = string.Empty;
}
