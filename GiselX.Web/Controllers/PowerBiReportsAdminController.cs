using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

[Area("Admin")]
[Authorize(Roles = "Administrator")]
public sealed class PowerBiReportsAdminController(IPowerBiReportService service) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int? companyId, CancellationToken token)
    {
        var reports = await service.ListAdminAsync(companyId, token);
        var companies = await service.ListCompanyOptionsAsync(token);
        return View(new PowerBiReportAdminIndexViewModel(reports.Data ?? [], companies.Data ?? [], companyId));
    }

    [HttpGet]
    public async Task<IActionResult> Create(int? companyId, CancellationToken token)
    {
        var model = new PowerBiReportEditViewModel { CompanyId = companyId ?? 0 };
        if (companyId is > 0)
        {
            var nextOrder = await service.GetNextDisplayOrderAsync(companyId.Value, token);
            if (nextOrder.IsSuccess) model.DisplayOrder = nextOrder.Data;
        }

        return View("Edit", await WithCompaniesAsync(model, token));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken token)
    {
        var report = await service.GetAdminAsync(id, token);
        if (report.Data is null) return NotFound();

        return View(await WithCompaniesAsync(ToEditModel(report.Data), token));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(PowerBiReportEditViewModel model, CancellationToken token)
    {
        if (!ModelState.IsValid) return View("Edit", await WithCompaniesAsync(model, token));

        var response = await service.SaveAsync(new SavePowerBiReportDto
        {
            Id = model.Id,
            CompanyId = model.CompanyId,
            Name = model.Name,
            EmbedUrl = model.EmbedUrl,
            DisplayOrder = model.DisplayOrder,
            IsActive = model.IsActive
        }, Username, token);
        if (response.StatusCode == StatusCodes.Status404NotFound) return NotFound();
        if (!response.IsSuccess)
        {
            AddFailureErrors(response, "Unable to save the Power BI report.");
            return View("Edit", await WithCompaniesAsync(model, token));
        }

        TempData["StatusMessage"] = response.Message;
        return RedirectToAction(nameof(Index), new { companyId = model.CompanyId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Activate(int id, int? companyId, CancellationToken token) =>
        await UpdateStateAsync(id, true, companyId, token);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deactivate(int id, int? companyId, CancellationToken token) =>
        await UpdateStateAsync(id, false, companyId, token);

    [HttpGet]
    public async Task<IActionResult> Delete(int id, int? companyId, CancellationToken token)
    {
        var response = await service.GetAdminAsync(id, token);
        if (response.Data is null) return NotFound();

        return View(new PowerBiReportDeleteViewModel
        {
            Id = response.Data.Id,
            CompanyId = companyId,
            CompanyName = response.Data.CompanyName,
            ReportName = response.Data.Name,
            EmbedUrl = response.Data.EmbedUrl
        });
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id, int? companyId, CancellationToken token)
    {
        var response = await service.DeleteAsync(id, token);
        if (response.StatusCode == StatusCodes.Status404NotFound) return NotFound();
        if (response.IsSuccess)
            TempData["StatusMessage"] = response.Message;
        else
            TempData["ErrorMessage"] = FailureMessage(response, "Unable to delete the Power BI report.");
        return RedirectToAction(nameof(Index), new { companyId });
    }

    [HttpGet]
    public async Task<IActionResult> Preview(int id, CancellationToken token)
    {
        var response = await service.GetAdminAsync(id, token);
        if (response.Data is null) return NotFound();

        return View("~/Views/PowerBiReports/View.cshtml", new PowerBiReportViewerDto(
            response.Data.Id, response.Data.Name, response.Data.EmbedUrl));
    }

    private string Username => ControllerContext.HttpContext?.User.Identity?.Name ?? "unknown";

    private async Task<IActionResult> UpdateStateAsync(int id, bool isActive, int? companyId, CancellationToken token)
    {
        var response = await service.SetActiveAsync(id, isActive, Username, token);
        if (response.StatusCode == StatusCodes.Status404NotFound) return NotFound();
        if (response.IsSuccess)
            TempData["StatusMessage"] = response.Message;
        else
            TempData["ErrorMessage"] = FailureMessage(response, "Unable to update the Power BI report.");
        return RedirectToAction(nameof(Index), new { companyId });
    }

    private async Task<PowerBiReportEditViewModel> WithCompaniesAsync(PowerBiReportEditViewModel model,
        CancellationToken token)
    {
        var response = await service.ListCompanyOptionsAsync(token);
        model.Companies = response.Data ?? [];
        return model;
    }

    private static PowerBiReportEditViewModel ToEditModel(PowerBiReportDto report) => new()
    {
        Id = report.Id,
        CompanyId = report.CompanyId,
        Name = report.Name,
        EmbedUrl = report.EmbedUrl,
        DisplayOrder = report.DisplayOrder,
        IsActive = report.IsActive
    };

    private void AddFailureErrors(ServiceResponse response, string fallback)
    {
        var errors = response.Errors.Where(error => !string.IsNullOrWhiteSpace(error))
            .Distinct(StringComparer.Ordinal).ToList();
        if (errors.Count > 0)
        {
            foreach (var error in errors) ModelState.AddModelError(string.Empty, error);
            return;
        }

        ModelState.AddModelError(string.Empty, FailureMessage(response, fallback));
    }

    private static string FailureMessage(ServiceResponse response, string fallback) =>
        string.IsNullOrWhiteSpace(response.Message) ? fallback : response.Message;
}
