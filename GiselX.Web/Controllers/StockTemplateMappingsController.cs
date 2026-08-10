using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(PermissionConstants.Companies.Edit)]
public sealed class StockTemplateMappingsController(
    IStockTemplateMappingService mappingService,
    IStockWorkbookInspector inspector) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int companyId, CancellationToken cancellationToken)
    {
        var response = await mappingService.ListAsync(companyId, cancellationToken);
        return View(new StockTemplateMappingListViewModel { CompanyId = companyId, Mappings = response.Data ?? [] });
    }

    [HttpGet]
    public IActionResult Create(int companyId) =>
        View("Edit", new StockTemplateMappingEditViewModel { CompanyId = companyId });

    [HttpGet]
    public async Task<IActionResult> Edit(int id, int companyId, CancellationToken cancellationToken)
    {
        var value = (await mappingService.GetAsync(id, companyId, cancellationToken)).Data;
        if (value is null) return NotFound();
        return View(new StockTemplateMappingEditViewModel
        {
            Id = value.Id,
            CompanyId = value.CompanyId,
            Name = value.Name,
            WorksheetName = value.WorksheetName,
            HeaderRowNumber = value.HeaderRowNumber,
            FirstDataRowNumber = value.FirstDataRowNumber,
            Columns = value.Columns.Select(column => new StockTemplateColumnEditViewModel
            {
                SourceColumnNumber = column.SourceColumnNumber,
                SourceHeader = column.SourceHeader,
                CanonicalField = column.CanonicalField
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Inspect(IFormFile sampleFile, string? worksheetName, int headerRowNumber,
        int firstDataRowNumber, CancellationToken cancellationToken)
    {
        if (sampleFile is null || sampleFile.Length == 0) return BadRequest("A sample .xlsx file is required.");
        return Json(await inspector.InspectAsync(await ReadAsync(sampleFile, cancellationToken), worksheetName,
            headerRowNumber, firstDataRowNumber, cancellationToken));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(StockTemplateMappingEditViewModel model, IFormFile sampleFile,
        CancellationToken cancellationToken)
    {
        if (sampleFile is null || sampleFile.Length == 0)
            ModelState.AddModelError("sampleFile", "A sample .xlsx file is required.");
        if (!ModelState.IsValid) return View("Edit", model);

        var preview = await inspector.InspectAsync(await ReadAsync(sampleFile!, cancellationToken),
            model.WorksheetName, model.HeaderRowNumber, model.FirstDataRowNumber, cancellationToken);
        if (preview.Errors.Count > 0)
        {
            foreach (var error in preview.Errors) ModelState.AddModelError(string.Empty, error);
            return View("Edit", model);
        }

        var assignments = model.Columns.Where(column => column.CanonicalField.HasValue)
            .Where(column => column.SourceColumnNumber > 0 && column.SourceColumnNumber <= preview.Headers.Count)
            .Select(column => new StockTemplateColumnDto
            {
                SourceColumnNumber = column.SourceColumnNumber,
                SourceHeader = preview.Headers[column.SourceColumnNumber - 1],
                CanonicalField = column.CanonicalField!.Value
            }).ToList();
        var response = await mappingService.SaveAsync(new SaveStockTemplateMappingDto
        {
            Id = model.Id,
            CompanyId = model.CompanyId,
            Name = model.Name,
            WorksheetName = preview.WorksheetName ?? string.Empty,
            HeaderRowNumber = model.HeaderRowNumber,
            FirstDataRowNumber = model.FirstDataRowNumber,
            HeaderSignature = preview.HeaderSignature,
            Columns = assignments
        }, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!response.IsSuccess)
        {
            foreach (var error in response.Errors) ModelState.AddModelError(string.Empty, error);
            return View("Edit", model);
        }

        return RedirectToAction("Edit", "Companies", new { area = "Admin", id = model.CompanyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, true,
            User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.SetActiveAsync(id, companyId, false,
            User.Identity?.Name ?? "unknown", cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await mappingService.DeleteAsync(id, companyId, cancellationToken));

    private IActionResult RedirectToList(int companyId, ServiceResponse response)
    {
        if (response.IsSuccess) TempData["StatusMessage"] = response.Message;
        else TempData["ErrorMessage"] = string.Join(" ", response.Errors);
        return RedirectToAction(nameof(Index), new { companyId });
    }

    private static async Task<byte[]> ReadAsync(IFormFile file, CancellationToken cancellationToken)
    {
        await using var input = file.OpenReadStream();
        using var memory = new MemoryStream();
        await input.CopyToAsync(memory, cancellationToken);
        return memory.ToArray();
    }
}
