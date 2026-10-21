using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

/// <summary>
/// The per-company master of Stock areas (locations a distributor reports stock for). Every route
/// carries <c>companyId</c> and every query is scoped by it. Permission is Companies.Edit, because
/// areas are part of a company's setup.
/// </summary>
[Area("Admin")]
[Authorize]
public sealed class StockAreasController(IStockAreaService service, ICompanyService companyService) : Controller
{
    [HttpGet]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Index(int companyId, CancellationToken cancellationToken)
    {
        if (!await LoadCompanyAsync(companyId, cancellationToken)) return NotFound();
        return View((await service.ListAsync(companyId, cancellationToken)).Data ?? []);
    }

    [HttpGet]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Create(int companyId, CancellationToken cancellationToken)
    {
        if (!await LoadCompanyAsync(companyId, cancellationToken)) return NotFound();
        return View("Edit", new StockAreaDto { CompanyId = companyId });
    }

    [HttpGet]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Edit(int id, int companyId, CancellationToken cancellationToken)
    {
        if (!await LoadCompanyAsync(companyId, cancellationToken)) return NotFound();
        var value = (await service.GetAsync(id, companyId, cancellationToken)).Data;
        return value is null ? NotFound() : View(value);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Save(StockAreaDto model, CancellationToken cancellationToken)
    {
        if (!await LoadCompanyAsync(model.CompanyId, cancellationToken)) return NotFound();
        if (!ModelState.IsValid) return View("Edit", model);
        var response = await service.SaveAsync(model, Username, cancellationToken);
        if (!response.IsSuccess)
        {
            foreach (var error in response.Errors) ModelState.AddModelError(string.Empty, error);
            return View("Edit", model);
        }
        TempData["StatusMessage"] = response.Message;
        return RedirectToAction(nameof(Index), new { companyId = model.CompanyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Activate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await service.SetActiveAsync(id, companyId, true, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Deactivate(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await service.SetActiveAsync(id, companyId, false, Username, cancellationToken));

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Delete(int id, int companyId, CancellationToken cancellationToken) =>
        RedirectToList(companyId, await service.DeleteAsync(id, companyId, cancellationToken));

    private string Username => User.Identity?.Name ?? "unknown";

    private async Task<bool> LoadCompanyAsync(int companyId, CancellationToken cancellationToken)
    {
        var company = (await companyService.GetCompanyByIdAsync(companyId, cancellationToken)).Data;
        if (company is null) return false;
        ViewBag.CompanyId = company.Id;
        ViewBag.CompanyName = company.Name;
        return true;
    }

    private IActionResult RedirectToList(int companyId, ServiceResponse response)
    {
        if (response.IsSuccess) TempData["StatusMessage"] = response.Message;
        else TempData["ErrorMessage"] = string.Join(" ", response.Errors);
        return RedirectToAction(nameof(Index), new { companyId });
    }
}
