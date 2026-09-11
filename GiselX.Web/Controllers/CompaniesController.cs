using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace GiselX.Web.Controllers;

[Area("Admin")]
public class CompaniesController : Controller
{
    private readonly ICompanyService _companyService;
    private readonly IBusinessUnitService? _businessUnitService;
    private readonly IChannelService? _channelService;
    private readonly ISalesmanService? _salesmanService;

    public CompaniesController(ICompanyService companyService, IBusinessUnitService? businessUnitService = null,
        IChannelService? channelService = null, ISalesmanService? salesmanService = null)
    {
        _companyService = companyService;
        _businessUnitService = businessUnitService;
        _channelService = channelService;
        _salesmanService = salesmanService;
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
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        await LoadLookupsAsync(null, cancellationToken);
        return View();
    }

    // POST: CompaniesController/Create
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Companies.Create)]
    public async Task<IActionResult> Create(CompanyDto model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            await LoadLookupsAsync(model, cancellationToken);
            return View(model);
        }
        var result = await _companyService.CreateCompanyAsync(model, cancellationToken);
        if (result.StatusCode != 200)
        {
            AddFailureErrors(result, "Unable to create the company. Please try again.");
            await LoadLookupsAsync(model, cancellationToken);
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
        result.Data.ItemMappingEnabled ??= false;
        await LoadLookupsAsync(result.Data, cancellationToken);
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
        {
            await LoadLookupsAsync(model, cancellationToken);
            return View(model);
        }
        var result = await _companyService.UpdateCompanyAsync(model, cancellationToken);
        if (result.StatusCode != 200)
        {
            AddFailureErrors(result, "Unable to update the company. Please try again.");
            await LoadLookupsAsync(model, cancellationToken);
            return View(model);
        }
        TempData["StatusMessage"] = result.Message;
        return RedirectToAction(nameof(Index));
    }

    private void AddFailureErrors(ServiceResponse response, string fallback)
    {
        var errors = response.Errors.Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal).ToList();
        if (errors.Count > 0)
        {
            foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
            return;
        }

        ModelState.AddModelError(string.Empty,
            string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message);
    }

    private async Task LoadLookupsAsync(CompanyDto? company, CancellationToken cancellationToken)
    {
        if (_businessUnitService is not null)
        {
            var response = await _businessUnitService.ListAsync(false, cancellationToken);
            var values = (response.Data ?? [])
                .Where(value => value.IsActive || value.Id == company?.BusinessUnitId).ToList();
            ViewBag.BusinessUnitOptions = new SelectList(values, nameof(BusinessUnitDto.Id),
                nameof(BusinessUnitDto.Name), company?.BusinessUnitId);
        }

        if (_channelService is not null)
        {
            var response = await _channelService.ListAsync(false, cancellationToken);
            var values = (response.Data ?? [])
                .Where(value => value.IsActive || value.Id == company?.ChannelId).ToList();
            ViewBag.ChannelOptions = new SelectList(values, nameof(ChannelDto.Id),
                nameof(ChannelDto.Name), company?.ChannelId);
        }

        if (_salesmanService is not null)
        {
            var response = await _salesmanService.ListAsync(false, cancellationToken);
            var values = (response.Data ?? [])
                .Where(value => value.IsActive || value.Id == company?.SalesmanId).ToList();
            ViewBag.SalesmanOptions = new SelectList(values, nameof(SalesmanDto.Id),
                nameof(SalesmanDto.Name), company?.SalesmanId);
        }
    }
}
