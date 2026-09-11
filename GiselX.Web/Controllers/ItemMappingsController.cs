using ClosedXML.Excel;
using GiselX.Common.Constants;
using GiselX.Service;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(PermissionConstants.Companies.Edit)]
public sealed class ItemMappingsController(IItemMappingService mappingService, IItemMappingImportService importService)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int companyId, string? search, string? sortBy, string? sortDirection,
        CancellationToken cancellationToken)
    {
        var response = await mappingService.ListAsync(companyId, cancellationToken);
        var unmapped = await mappingService.ListUnmappedAsync(companyId, cancellationToken);
        var mappings = FilterAndSort(response.Data ?? [], search, sortBy, sortDirection);
        return View(new ItemMappingListViewModel
        {
            CompanyId = companyId,
            Search = search,
            SortBy = sortBy,
            SortDirection = sortDirection,
            Mappings = mappings,
            UnmappedItems = Filter(unmapped.Data?.Items ?? [], search),
            ItemMappingEnabled = unmapped.Data?.ItemMappingEnabled == true
        });
    }

    [HttpGet]
    public IActionResult Create(int companyId, string? distributorItemId = null, string? distributorItemName = null) =>
        View("Edit", new ItemMappingEditViewModel
        {
            CompanyId = companyId,
            DistributorItemId = distributorItemId ?? string.Empty,
            DistributorItemName = distributorItemName ?? string.Empty
        });

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int companyId, CancellationToken cancellationToken)
    {
        var mapping = (await mappingService.GetAsync(id, companyId, cancellationToken)).Data;
        if (mapping is null) return NotFound();
        return View(new ItemMappingEditViewModel
        {
            Id = mapping.Id,
            CompanyId = mapping.CompanyId,
            GmkItemId = mapping.GmkItemId,
            GmkItemName = mapping.GmkItemName,
            DistributorItemId = mapping.DistributorItemId,
            DistributorItemName = mapping.DistributorItemName,
            IsActive = mapping.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ItemMappingEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid) return View("Edit", model);
        var response = await mappingService.SaveAsync(new SaveItemMappingDto
        {
            Id = model.Id,
            CompanyId = model.CompanyId,
            GmkItemId = model.GmkItemId,
            GmkItemName = model.GmkItemName,
            DistributorItemId = model.DistributorItemId,
            DistributorItemName = model.DistributorItemName,
            IsActive = model.IsActive
        }, Username, cancellationToken);
        if (!response.IsSuccess)
        {
            foreach (var error in response.Errors) ModelState.AddModelError(string.Empty, error);
            return View("Edit", model);
        }

        TempData["StatusMessage"] = response.Message;
        return RedirectToAction(nameof(Index), new { model.CompanyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, true, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, false, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.DeleteAsync(id, companyId, cancellationToken));

    [HttpGet]
    public IActionResult DownloadTemplate(int companyId)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Item Mappings");
        worksheet.Cell(1, 1).Value = "GMK Item ID";
        worksheet.Cell(1, 2).Value = "GMK Item Name";
        worksheet.Cell(1, 3).Value = "Distributor Item ID";
        worksheet.Cell(1, 4).Value = "Distributor Item Name";
        worksheet.Cell(1, 5).Value = "Status";
        worksheet.Row(1).Style.Font.Bold = true;
        worksheet.Columns().AdjustToContents();
        var instructions = workbook.Worksheets.Add("Instructions");
        instructions.Cell(1, 1).Value = "Status";
        instructions.Cell(1, 2).Value = "Meaning";
        instructions.Cell(2, 1).Value = 1;
        instructions.Cell(2, 2).Value = "Active";
        instructions.Cell(3, 1).Value = 0;
        instructions.Cell(3, 2).Value = "Inactive";
        instructions.Row(1).Style.Font.Bold = true;
        instructions.Columns().AdjustToContents();
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"item-mapping-template-company-{companyId}.xlsx");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PreflightImport(int companyId, IFormFile file, CancellationToken cancellationToken)
    {
        var workbook = await ReadWorkbookAsync(file, cancellationToken);
        if (workbook.Error is not null) return BadRequest(InvalidWorkbook(workbook.Error));
        var response = await importService.PreflightAsync(workbook.Content!, companyId,
            cancellationToken);
        return response.IsSuccess ? Json(response) : BadRequest(response);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Import(int companyId, IFormFile file, CancellationToken cancellationToken)
    {
        var workbook = await ReadWorkbookAsync(file, cancellationToken);
        if (workbook.Error is not null) return BadRequest(InvalidWorkbook(workbook.Error));
        var response = await importService.ImportAsync(workbook.Content!, companyId, true,
            Username, cancellationToken);
        return response.IsSuccess ? Json(response) : BadRequest(response);
    }

    private string Username => User.Identity?.Name ?? "unknown";

    private IActionResult RedirectToList(int companyId, ServiceResponse response)
    {
        if (response.IsSuccess) TempData["StatusMessage"] = response.Message;
        else TempData["ErrorMessage"] = string.Join(" ", response.Errors);
        return RedirectToAction(nameof(Index), new { companyId });
    }

    private static async Task<WorkbookUpload> ReadWorkbookAsync(IFormFile? file, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0) return new WorkbookUpload(null, "An .xlsx or .xls file is required.");
        if (!LegacyExcelWorkbookConverter.IsSupportedExtension(file.FileName))
            return new WorkbookUpload(null, "Upload an .xlsx or .xls file.");
        await using var source = file.OpenReadStream();
        using var destination = new MemoryStream();
        await source.CopyToAsync(destination, cancellationToken);
        byte[] content;
        try
        {
            content = LegacyExcelWorkbookConverter.EnsureOpenXml(destination.ToArray());
        }
        catch (InvalidDataException exception)
        {
            return new WorkbookUpload(null, exception.Message);
        }

        try
        {
            using var stream = new MemoryStream(content);
            using var document = new XLWorkbook(stream);
            if (!document.Worksheets.Any()) return new WorkbookUpload(null, "Upload a valid .xlsx or .xls workbook.");
            return new WorkbookUpload(content, null);
        }
        catch (Exception)
        {
            return new WorkbookUpload(null, "Upload a valid .xlsx or .xls workbook.");
        }
    }

    private static ServiceResponse InvalidWorkbook(string error) => new()
    {
        Errors = [error],
        StatusCode = StatusCodes.Status400BadRequest
    };

    private static IReadOnlyList<ItemMappingDto> FilterAndSort(IReadOnlyList<ItemMappingDto> mappings, string? search,
        string? sortBy, string? sortDirection)
    {
        IEnumerable<ItemMappingDto> values = mappings;
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            values = values.Where(mapping => Contains(mapping.GmkItemId, term) || Contains(mapping.GmkItemName, term) ||
                Contains(mapping.DistributorItemId, term) || Contains(mapping.DistributorItemName, term));
        }

        var descending = string.Equals(sortDirection, "desc", StringComparison.OrdinalIgnoreCase);
        return (sortBy?.ToLowerInvariant(), descending) switch
        {
            ("gmkitemid", false) => values.OrderBy(mapping => mapping.GmkItemId, StringComparer.OrdinalIgnoreCase).ToList(),
            ("gmkitemid", true) => values.OrderByDescending(mapping => mapping.GmkItemId, StringComparer.OrdinalIgnoreCase).ToList(),
            ("gmkitemname", false) => values.OrderBy(mapping => mapping.GmkItemName, StringComparer.OrdinalIgnoreCase).ToList(),
            ("gmkitemname", true) => values.OrderByDescending(mapping => mapping.GmkItemName, StringComparer.OrdinalIgnoreCase).ToList(),
            ("distributoritemname", false) => values.OrderBy(mapping => mapping.DistributorItemName, StringComparer.OrdinalIgnoreCase).ToList(),
            ("distributoritemname", true) => values.OrderByDescending(mapping => mapping.DistributorItemName, StringComparer.OrdinalIgnoreCase).ToList(),
            ("status", false) => values.OrderBy(mapping => mapping.IsActive).ToList(),
            ("status", true) => values.OrderByDescending(mapping => mapping.IsActive).ToList(),
            ("distributoritemid", true) => values.OrderByDescending(mapping => mapping.DistributorItemId, StringComparer.OrdinalIgnoreCase).ToList(),
            _ => values.OrderBy(mapping => mapping.DistributorItemId, StringComparer.OrdinalIgnoreCase).ToList()
        };
    }

    private static IReadOnlyList<UnmappedDistributorItemDto> Filter(IReadOnlyList<UnmappedDistributorItemDto> items,
        string? search)
    {
        if (string.IsNullOrWhiteSpace(search)) return items;
        var term = search.Trim();
        return items.Where(item => Contains(item.DistributorItemId, term) || Contains(item.DistributorItemName, term))
            .ToList();
    }

    private static bool Contains(string? value, string term) => value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    private sealed record WorkbookUpload(byte[]? Content, string? Error);
}
