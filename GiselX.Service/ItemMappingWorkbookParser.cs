using ClosedXML.Excel;
using GiselX.Domain;
using GiselX.Service.Dto;

namespace GiselX.Service;

public sealed class ItemMappingWorkbookParser
{
    private static readonly string[] Headers =
    [
        "GMK Item ID", "GMK Item Name", "Distributor Item ID", "Distributor Item Name"
    ];

    public ItemMappingWorkbookParseResult Parse(byte[] workbook)
    {
        using var stream = new MemoryStream(workbook);
        using var document = new XLWorkbook(stream);
        var worksheet = document.Worksheets.First();
        var errors = ValidateHeaders(worksheet);
        if (errors.Count > 0) return new ItemMappingWorkbookParseResult([], errors, 0, 0);

        var sourceRows = new List<(int RowNumber, ItemMapping Mapping)>();
        var invalidRows = new HashSet<int>();
        var totalRows = 0;
        var lastUsedRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 2; rowNumber <= lastUsedRow; rowNumber++)
        {
            var values = Enumerable.Range(1, Headers.Length)
                .Select(column => worksheet.Cell(rowNumber, column).GetString().Trim())
                .ToArray();
            if (values.All(string.IsNullOrWhiteSpace)) continue;
            totalRows++;

            var rowErrors = ValidateValues(rowNumber, values);
            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
                invalidRows.Add(rowNumber);
                continue;
            }

            sourceRows.Add((rowNumber, new ItemMapping
            {
                GmkItemId = values[0], GmkItemName = values[1], DistributorItemId = values[2],
                DistributorItemName = values[3], CreatedDate = DateTime.UtcNow
            }));
        }

        foreach (var duplicate in sourceRows.GroupBy(row => Normalize(row.Mapping.DistributorItemId))
                     .Where(group => group.Count() > 1))
        {
            foreach (var sourceRow in duplicate)
            {
                errors.Add(new ItemMappingImportErrorDto
                {
                    SourceRowNumber = sourceRow.RowNumber, Field = "Distributor Item ID",
                    RejectedValue = sourceRow.Mapping.DistributorItemId,
                    Reason = "Distributor Item ID is duplicate in the workbook."
                });
                invalidRows.Add(sourceRow.RowNumber);
            }
        }

        var validRows = sourceRows.Where(row => !invalidRows.Contains(row.RowNumber))
            .Select(row => row.Mapping).ToList();
        return new ItemMappingWorkbookParseResult(validRows, errors, totalRows, invalidRows.Count);
    }

    internal static string Normalize(string value) => value.Trim().ToUpperInvariant();

    private static List<ItemMappingImportErrorDto> ValidateHeaders(IXLWorksheet worksheet)
    {
        var errors = new List<ItemMappingImportErrorDto>();
        for (var index = 0; index < Headers.Length; index++)
        {
            if (!string.Equals(worksheet.Cell(1, index + 1).GetString().Trim(), Headers[index], StringComparison.Ordinal))
            {
                errors.Add(new ItemMappingImportErrorDto
                {
                    SourceRowNumber = 1, Field = Headers[index],
                    Reason = $"Expected header '{Headers[index]}'."
                });
            }
        }

        return errors;
    }

    private static List<ItemMappingImportErrorDto> ValidateValues(int rowNumber, IReadOnlyList<string> values)
    {
        var errors = new List<ItemMappingImportErrorDto>();
        for (var index = 0; index < values.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(values[index]))
            {
                errors.Add(Error(rowNumber, index, values[index], "A value is required."));
                continue;
            }

            var maximumLength = index is 0 or 2 ? 100 : 200;
            if (values[index].Length > maximumLength)
            {
                errors.Add(Error(rowNumber, index, values[index],
                    $"Value exceeds the maximum length of {maximumLength}."));
            }
        }

        return errors;
    }

    private static ItemMappingImportErrorDto Error(int rowNumber, int index, string rejectedValue, string reason) => new()
    {
        SourceRowNumber = rowNumber, Field = Headers[index], RejectedValue = rejectedValue, Reason = reason
    };
}

public sealed class ItemMappingWorkbookParseResult(
    IReadOnlyList<ItemMapping> rows,
    IReadOnlyList<ItemMappingImportErrorDto> errors,
    int totalRows,
    int invalidRows)
{
    public IReadOnlyList<ItemMapping> Rows { get; } = rows;
    public IReadOnlyList<ItemMappingImportErrorDto> Errors { get; } = errors;
    public int TotalRows { get; } = totalRows;
    public int ValidRows => TotalRows - InvalidRows;
    public int InvalidRows { get; } = invalidRows;
}
