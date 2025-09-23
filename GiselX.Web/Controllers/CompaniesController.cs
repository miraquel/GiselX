using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
public class CompaniesController : Controller
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [Authorize(PermissionConstants.Companies.Index)]
    // GET: CompaniesController
    public IActionResult Index(CancellationToken cancellationToken)
    {
        // The view uses DataTables and fetches data via API, so just return the view
        return View();
    }

    [Authorize(PermissionConstants.Companies.Details)]
    // GET: CompaniesController/Details/5
    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        if (result.Data == null)
            return NotFound();
        return View(result.Data);
    }

    [Authorize(PermissionConstants.Companies.Create)]
    // GET: CompaniesController/Create
    public IActionResult Create()
    {
        return View();
    }

    // POST: CompaniesController/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Create)]
    public async Task<IActionResult> Create(CompanyDto model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);
        var result = await _companyService.CreateCompanyAsync(model, cancellationToken);
        if (result.StatusCode != 200)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? string.Empty);
            return View(model);
        }
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    [Authorize(PermissionConstants.Companies.Edit)]
    // GET: CompaniesController/Edit/5
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var result = await _companyService.GetCompanyByIdAsync(id, cancellationToken);
        if (result.Data == null)
            return NotFound();
        return View(result.Data);
    }

    // POST: CompaniesController/Edit/5
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Edit)]
    public async Task<IActionResult> Edit(int id, CompanyDto model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();
        if (!ModelState.IsValid)
            return View(model);
        var result = await _companyService.UpdateCompanyAsync(model, cancellationToken);
        if (result.StatusCode != 200)
        {
            ModelState.AddModelError(string.Empty, result.Message ?? string.Empty);
            return View(model);
        }
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }
}