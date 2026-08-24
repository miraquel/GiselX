using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;
using GiselX.Service.Dto;
using GiselX.Service.Interface;

namespace GiselX.Service;

public sealed class XlsxSelloutWorkbookInspector : ISelloutWorkbookInspector
{
    private static readonly XNamespace Spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    private static readonly XNamespace OfficeRelationship = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private static readonly XNamespace PackageRelationship = "http://schemas.openxmlformats.org/package/2006/relationships";
    private const int SampleRowLimit = 5;

    public Task<SelloutWorkbookPreviewDto> InspectAsync(
        byte[] workbook,
        string? worksheetName,
        int headerRowNumber,
        int firstDataRowNumber,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var result = new SelloutWorkbookPreviewDto
        {
            WorksheetName = worksheetName,
            HeaderRowNumber = headerRowNumber,
            FirstDataRowNumber = firstDataRowNumber
        };

        if (headerRowNumber < 1)
        {
            result.Errors.Add("Header row number must be at least 1.");
        }
        if (firstDataRowNumber <= headerRowNumber)
        {
            result.Errors.Add("First data row number must be after the header row.");
        }
        if (workbook.Length == 0)
        {
            result.Errors.Add("The workbook is empty.");
            return Task.FromResult(result);
        }

        try
        {
            using var stream = new MemoryStream(workbook, writable: false);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            var sheets = ReadSheets(archive);
            result.WorksheetNames = sheets.Select(sheet => sheet.Name).ToList();

            var selected = string.IsNullOrWhiteSpace(worksheetName)
                ? sheets.FirstOrDefault()
                : sheets.FirstOrDefault(sheet => string.Equals(sheet.Name, worksheetName, StringComparison.Ordinal));
            if (selected is null)
            {
                result.Errors.Add($"Worksheet '{worksheetName}' was not found.");
                return Task.FromResult(result);
            }

            result.WorksheetName = selected.Name;
            if (result.Errors.Count > 0)
            {
                return Task.FromResult(result);
            }

            var sharedStrings = ReadSharedStrings(archive);
            var worksheet = ReadWorksheetData(archive, selected.Path, sharedStrings);
            var rows = ExpandVerticalMergedValues(worksheet, firstDataRowNumber);
            worksheet.Rows.TryGetValue(headerRowNumber, out var headerCells);
            headerCells ??= new Dictionary<int, string>();
            var width = headerCells.Count == 0 ? 0 : headerCells.Keys.Max();
            result.Headers = Enumerable.Range(1, width)
                .Select(column => headerCells.GetValueOrDefault(column, string.Empty)).ToList();
            result.NormalizedHeaders = result.Headers.Select(SelloutHeaderNormalizer.Normalize).ToList();
            result.HeaderSignature = SelloutHeaderNormalizer.CreateSignature(result.Headers);
            var mergedHeaderContinuationColumns = worksheet.MergedRanges
                .Where(range => range.FirstRow == headerRowNumber && range.LastColumn > range.FirstColumn)
                .SelectMany(range => Enumerable.Range(range.FirstColumn + 1, range.LastColumn - range.FirstColumn))
                .ToHashSet();
            result.Errors.AddRange(SelloutHeaderNormalizer.ValidateHeaders(
                result.Headers,
                mergedHeaderContinuationColumns));
            if (result.Headers.Count == 0)
            {
                result.Errors.Add($"No headers were found on row {headerRowNumber}.");
            }

            result.SampleRows = rows
                .Where(row => row.Key >= firstDataRowNumber && row.Value.Values.Any(value => value.Length > 0))
                .OrderBy(row => row.Key)
                .Take(SampleRowLimit)
                .Select(row => Enumerable.Range(1, width)
                    .Select(column => row.Value.GetValueOrDefault(column, string.Empty)).ToList())
                .ToList();
        }
        catch (InvalidDataException)
        {
            result.Errors.Add("The file is not a valid .xlsx workbook.");
        }
        catch (Exception exception) when (exception is System.Xml.XmlException or FormatException)
        {
            result.Errors.Add("The workbook structure is invalid.");
        }

        return Task.FromResult(result);
    }

    public async Task<SelloutMappingDetectionResultDto> DetectAsync(
        byte[] workbook,
        IReadOnlyCollection<DistributorTemplateMappingDto> mappings,
        CancellationToken cancellationToken = default)
    {
        var matches = new List<DistributorTemplateMappingDto>();
        foreach (var mapping in mappings.Where(mapping => mapping.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var preview = await InspectAsync(
                workbook,
                mapping.WorksheetName,
                mapping.HeaderRowNumber,
                mapping.FirstDataRowNumber,
                cancellationToken);
            if (preview.Errors.Count == 0 && string.Equals(preview.HeaderSignature, mapping.HeaderSignature, StringComparison.Ordinal))
            {
                matches.Add(mapping);
            }
        }

        if (matches.Count == 1)
        {
            return new SelloutMappingDetectionResultDto { Mapping = matches[0] };
        }

        var result = new SelloutMappingDetectionResultDto
        {
            CandidateMappingNames = matches.Select(mapping => mapping.Name).OrderBy(name => name, StringComparer.Ordinal).ToList()
        };
        result.Errors.Add(matches.Count == 0
            ? "No active distributor mapping exactly matches this workbook."
            : "Multiple active distributor mappings exactly match this workbook.");
        return result;
    }

    internal static IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> ReadWorksheet(
        byte[] workbook,
        string worksheetName,
        int firstDataRowNumber = 1)
    {
        using var stream = new MemoryStream(workbook, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sheet = ReadSheets(archive).FirstOrDefault(item => string.Equals(item.Name, worksheetName, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Worksheet '{worksheetName}' was not found.");
        var worksheet = ReadWorksheetData(archive, sheet.Path, ReadSharedStrings(archive));
        return ExpandVerticalMergedValues(worksheet, firstDataRowNumber);
    }

    internal static WorksheetData ReadWorksheetData(byte[] workbook, string worksheetName)
    {
        using var stream = new MemoryStream(workbook, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var sheet = ReadSheets(archive).FirstOrDefault(item => string.Equals(item.Name, worksheetName, StringComparison.Ordinal))
            ?? throw new InvalidDataException($"Worksheet '{worksheetName}' was not found.");
        return ReadWorksheetData(archive, sheet.Path, ReadSharedStrings(archive));
    }

    private static List<WorksheetDefinition> ReadSheets(ZipArchive archive)
    {
        var workbook = ReadXml(archive, "xl/workbook.xml");
        var relationships = ReadXml(archive, "xl/_rels/workbook.xml.rels")
            .Root?.Elements(PackageRelationship + "Relationship")
            .ToDictionary(
                element => (string?)element.Attribute("Id") ?? string.Empty,
                element => NormalizeWorksheetPath((string?)element.Attribute("Target") ?? string.Empty),
                StringComparer.Ordinal) ?? [];

        return workbook.Descendants(Spreadsheet + "sheet")
            .Select(sheet => new WorksheetDefinition(
                (string?)sheet.Attribute("name") ?? string.Empty,
                relationships.GetValueOrDefault((string?)sheet.Attribute(OfficeRelationship + "id") ?? string.Empty, string.Empty)))
            .Where(sheet => sheet.Name.Length > 0 && sheet.Path.Length > 0)
            .ToList();
    }

    private static List<string> ReadSharedStrings(ZipArchive archive)
    {
        var entry = archive.GetEntry("xl/sharedStrings.xml");
        if (entry is null)
        {
            return [];
        }

        using var stream = entry.Open();
        var document = XDocument.Load(stream);
        return document.Descendants(Spreadsheet + "si")
            .Select(item => string.Concat(item.Descendants(Spreadsheet + "t").Select(text => text.Value)))
            .ToList();
    }

    private static WorksheetData ReadWorksheetData(
        ZipArchive archive,
        string path,
        IReadOnlyList<string> sharedStrings)
    {
        var document = ReadXml(archive, path);
        var rows = new SortedDictionary<int, Dictionary<int, string>>();
        foreach (var row in document.Descendants(Spreadsheet + "row"))
        {
            if (!int.TryParse((string?)row.Attribute("r"), NumberStyles.None, CultureInfo.InvariantCulture, out var rowNumber))
            {
                continue;
            }

            var cells = new Dictionary<int, string>();
            foreach (var cell in row.Elements(Spreadsheet + "c"))
            {
                var column = ParseColumnNumber((string?)cell.Attribute("r"));
                if (column > 0)
                {
                    cells[column] = ReadCellValue(cell, sharedStrings);
                }
            }
            rows[rowNumber] = cells;
        }

        var mergedRanges = document.Descendants(Spreadsheet + "mergeCell")
            .Select(element => ParseRange((string?)element.Attribute("ref")))
            .Where(range => range is not null)
            .Cast<MergedRange>()
            .ToList();
        return new WorksheetData(
            rows.ToDictionary(row => row.Key, row => (IReadOnlyDictionary<int, string>)row.Value),
            mergedRanges);
    }

    internal static IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> ExpandVerticalMergedValues(
        WorksheetData worksheet,
        int firstDataRowNumber)
    {
        var rows = worksheet.Rows.ToDictionary(
            row => row.Key,
            row => new Dictionary<int, string>(row.Value));

        foreach (var range in worksheet.MergedRanges.Where(range =>
                     range.FirstRow >= firstDataRowNumber && range.LastRow > range.FirstRow))
        {
            if (!rows.TryGetValue(range.FirstRow, out var anchorRow) ||
                !anchorRow.TryGetValue(range.FirstColumn, out var anchorValue) ||
                string.IsNullOrWhiteSpace(anchorValue))
            {
                continue;
            }

            for (var rowNumber = range.FirstRow + 1; rowNumber <= range.LastRow; rowNumber++)
            {
                if (!rows.TryGetValue(rowNumber, out var row))
                {
                    row = [];
                    rows[rowNumber] = row;
                }

                if (string.IsNullOrWhiteSpace(row.GetValueOrDefault(range.FirstColumn)))
                {
                    row[range.FirstColumn] = anchorValue;
                }
            }
        }

        return rows.ToDictionary(
            row => row.Key,
            row => (IReadOnlyDictionary<int, string>)row.Value);
    }

    private static string ReadCellValue(XElement cell, IReadOnlyList<string> sharedStrings)
    {
        var type = (string?)cell.Attribute("t");
        if (type == "inlineStr")
        {
            return string.Concat(cell.Descendants(Spreadsheet + "t").Select(text => text.Value));
        }

        var value = cell.Element(Spreadsheet + "v")?.Value ?? string.Empty;
        if (type == "s" && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index >= 0 && index < sharedStrings.Count)
        {
            return sharedStrings[index];
        }
        return value;
    }

    private static int ParseColumnNumber(string? reference)
    {
        var result = 0;
        foreach (var character in reference ?? string.Empty)
        {
            if (!char.IsAsciiLetter(character))
            {
                break;
            }
            result = checked(result * 26 + char.ToUpperInvariant(character) - 'A' + 1);
        }
        return result;
    }

    private static MergedRange? ParseRange(string? reference)
    {
        var parts = (reference ?? string.Empty).Split(':', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !TryParseCellReference(parts[0], out var firstRow, out var firstColumn) ||
            !TryParseCellReference(parts[1], out var lastRow, out var lastColumn))
        {
            return null;
        }

        return new MergedRange(
            Math.Min(firstRow, lastRow),
            Math.Max(firstRow, lastRow),
            Math.Min(firstColumn, lastColumn),
            Math.Max(firstColumn, lastColumn));
    }

    private static bool TryParseCellReference(string reference, out int row, out int column)
    {
        column = ParseColumnNumber(reference);
        row = 0;
        var rowText = new string(reference.SkipWhile(char.IsAsciiLetter).ToArray());
        return column > 0 && int.TryParse(rowText, NumberStyles.None, CultureInfo.InvariantCulture, out row) && row > 0;
    }

    private static string NormalizeWorksheetPath(string target)
    {
        var path = target.Replace('\\', '/').TrimStart('/');
        return path.StartsWith("xl/", StringComparison.Ordinal) ? path : $"xl/{path}";
    }

    private static XDocument ReadXml(ZipArchive archive, string path)
    {
        var entry = archive.GetEntry(path) ?? throw new InvalidDataException($"Workbook part '{path}' was not found.");
        using var stream = entry.Open();
        return XDocument.Load(stream);
    }

    private sealed record WorksheetDefinition(string Name, string Path);

    internal sealed record WorksheetData(
        IReadOnlyDictionary<int, IReadOnlyDictionary<int, string>> Rows,
        IReadOnlyList<MergedRange> MergedRanges);

    internal sealed record MergedRange(int FirstRow, int LastRow, int FirstColumn, int LastColumn);
}
