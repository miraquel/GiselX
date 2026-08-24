using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using GiselX.Service;
using GiselX.Service.Dto;

namespace GiselX.Tests;

public sealed class ClosedXmlSelloutWorkbookInspectorTests
{
    internal static byte[] BuildWorkbook(string worksheetName, params (int Row, int Column, string Value)[] cells) =>
        CreateWorkbook(Sheet(worksheetName, cells));

    internal static byte[] BuildWorkbookWithMergedCells(
        string worksheetName,
        (int Row, int Column, string Value)[] cells,
        params string[] mergedRanges) =>
        CreateWorkbook(new SheetDefinition(worksheetName, cells, mergedRanges));

    [Fact]
    public async Task Inspect_reads_configured_sheet_header_and_sample_rows()
    {
        var workbook = CreateWorkbook(
            Sheet("Notes", (1, 1, "Ignore me")),
            Sheet("Sellout",
                (3, 1, " Customer ID "),
                (3, 2, "Customer   Name"),
                (5, 1, "C-01"),
                (5, 2, "Toko Satu")));

        var result = await new XlsxSelloutWorkbookInspector()
            .InspectAsync(workbook, "Sellout", 3, 5);

        Assert.Equal(["Notes", "Sellout"], result.WorksheetNames);
        Assert.Equal("Sellout", result.WorksheetName);
        Assert.Equal([" Customer ID ", "Customer   Name"], result.Headers);
        Assert.Equal(["CUSTOMER ID", "CUSTOMER NAME"], result.NormalizedHeaders);
        Assert.Single(result.SampleRows);
        Assert.Equal(["C-01", "Toko Satu"], result.SampleRows[0]);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Inspect_reports_missing_worksheet()
    {
        var workbook = CreateWorkbook(Sheet("Data"));

        var result = await new XlsxSelloutWorkbookInspector()
            .InspectAsync(workbook, "Missing", 1, 2);

        Assert.Contains(result.Errors, error => error.Contains("Missing", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Inspect_reports_duplicate_normalized_headers()
    {
        var workbook = CreateWorkbook(Sheet("Data",
            (1, 1, "Invoice No"),
            (1, 2, " invoice   no ")));

        var result = await new XlsxSelloutWorkbookInspector()
            .InspectAsync(workbook, "Data", 1, 2);

        Assert.Contains(result.Errors, error => error.Contains("duplicated", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Inspect_allows_blank_header_cells_covered_by_a_horizontal_merge()
    {
        var workbook = BuildWorkbookWithMergedCells("Data",
        [
            (1, 1, "Invoice Date"),
            (1, 4, "Product ID"),
            (2, 1, "2026-08-03"),
            (2, 4, "P-01")
        ], "A1:C1");

        var result = await new XlsxSelloutWorkbookInspector().InspectAsync(workbook, "Data", 1, 2);

        Assert.Equal(["Invoice Date", "", "", "Product ID"], result.Headers);
        Assert.Equal(["INVOICE DATE", "", "", "PRODUCT ID"], result.NormalizedHeaders);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Detect_returns_the_only_exact_active_mapping()
    {
        var workbook = CreateWorkbook(Sheet("Upload",
            (2, 1, "Customer ID"),
            (2, 2, "Invoice No")));
        var mappings = new[]
        {
            Mapping(1, "Matching", "Upload", 2, true, "Customer ID", "Invoice No"),
            Mapping(2, "Inactive", "Upload", 2, false, "Customer ID", "Invoice No"),
            Mapping(3, "Other", "Upload", 2, true, "Invoice No", "Customer ID")
        };

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(workbook, mappings);

        Assert.True(result.IsMatch);
        Assert.Equal(1, result.Mapping!.Id);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task Detect_rejects_when_no_mapping_matches()
    {
        var workbook = CreateWorkbook(Sheet("Upload", (1, 1, "Unknown")));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(
            workbook,
            [Mapping(1, "Known", "Upload", 1, true, "Customer ID")]);

        Assert.False(result.IsMatch);
        Assert.Null(result.Mapping);
        Assert.Contains(result.Errors, error => error.Contains("No active", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Detect_rejects_ambiguous_exact_mappings()
    {
        var workbook = CreateWorkbook(Sheet("Upload", (1, 1, "Customer ID")));

        var result = await new XlsxSelloutWorkbookInspector().DetectAsync(
            workbook,
            [
                Mapping(1, "First", "Upload", 1, true, "Customer ID"),
                Mapping(2, "Second", "Upload", 1, true, "Customer ID")
            ]);

        Assert.False(result.IsMatch);
        Assert.Null(result.Mapping);
        Assert.Equal(["First", "Second"], result.CandidateMappingNames);
        Assert.Contains(result.Errors, error => error.Contains("multiple", StringComparison.OrdinalIgnoreCase));
    }

    private static byte[] CreateWorkbook(params SheetDefinition[] sheets)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, true))
        {
            WriteEntry(archive, "[Content_Types].xml", new XDocument(
                new XElement(XName.Get("Types", "http://schemas.openxmlformats.org/package/2006/content-types"),
                    new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")),
                    new XElement(XName.Get("Default", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("Extension", "xml"), new XAttribute("ContentType", "application/xml")),
                    new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),
                    sheets.Select((_, index) => new XElement(XName.Get("Override", "http://schemas.openxmlformats.org/package/2006/content-types"), new XAttribute("PartName", $"/xl/worksheets/sheet{index + 1}.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"))))));
            WriteEntry(archive, "_rels/.rels", Relationships(("rId1", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument", "xl/workbook.xml")));

            XNamespace main = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            XNamespace rel = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
            WriteEntry(archive, "xl/workbook.xml", new XDocument(new XElement(main + "workbook",
                new XAttribute(XNamespace.Xmlns + "r", rel),
                new XElement(main + "sheets", sheets.Select((sheet, index) => new XElement(main + "sheet",
                    new XAttribute("name", sheet.Name), new XAttribute("sheetId", index + 1), new XAttribute(rel + "id", $"rId{index + 1}")))))));
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", Relationships(sheets.Select((_, index) =>
                ($"rId{index + 1}", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet", $"worksheets/sheet{index + 1}.xml")).ToArray()));

            foreach (var (sheet, index) in sheets.Select((value, index) => (value, index)))
            {
                var rows = sheet.Cells.GroupBy(cell => cell.Row).OrderBy(group => group.Key)
                    .Select(group => new XElement(main + "row", new XAttribute("r", group.Key),
                        group.OrderBy(cell => cell.Column).Select(cell => new XElement(main + "c",
                            new XAttribute("r", CellReference(cell.Row, cell.Column)), new XAttribute("t", "inlineStr"),
                            new XElement(main + "is", new XElement(main + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), cell.Value))))));
                var worksheet = new XElement(main + "worksheet",
                    new XElement(main + "sheetData", rows),
                    sheet.MergedRanges.Count == 0
                        ? null
                        : new XElement(main + "mergeCells",
                            new XAttribute("count", sheet.MergedRanges.Count),
                            sheet.MergedRanges.Select(range => new XElement(main + "mergeCell", new XAttribute("ref", range)))));
                WriteEntry(archive, $"xl/worksheets/sheet{index + 1}.xml", new XDocument(worksheet));
            }
        }

        return stream.ToArray();
    }

    private static SheetDefinition Sheet(string name, params (int Row, int Column, string Value)[] cells) => new(name, cells, []);

    private static XDocument Relationships(params (string Id, string Type, string Target)[] items)
    {
        XNamespace package = "http://schemas.openxmlformats.org/package/2006/relationships";
        return new XDocument(new XElement(package + "Relationships", items.Select(item => new XElement(package + "Relationship",
            new XAttribute("Id", item.Id), new XAttribute("Type", item.Type), new XAttribute("Target", item.Target)))));
    }

    private static void WriteEntry(ZipArchive archive, string name, XDocument document)
    {
        using var writer = new StreamWriter(archive.CreateEntry(name).Open(), new UTF8Encoding(false));
        document.Save(writer);
    }

    private static string CellReference(int row, int column)
    {
        var name = string.Empty;
        while (column > 0)
        {
            column--;
            name = (char)('A' + column % 26) + name;
            column /= 26;
        }
        return $"{name}{row}";
    }

    private sealed record SheetDefinition(
        string Name,
        IReadOnlyList<(int Row, int Column, string Value)> Cells,
        IReadOnlyList<string> MergedRanges);

    private static DistributorTemplateMappingDto Mapping(
        int id,
        string name,
        string worksheet,
        int headerRow,
        bool active,
        params string[] headers) => new()
        {
            Id = id,
            Name = name,
            WorksheetName = worksheet,
            HeaderRowNumber = headerRow,
            FirstDataRowNumber = headerRow + 1,
            IsActive = active,
            HeaderSignature = SelloutHeaderNormalizer.CreateSignature(headers)
        };
}
