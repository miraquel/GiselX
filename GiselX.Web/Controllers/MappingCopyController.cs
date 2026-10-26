using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

/// <summary>
/// Copies another company's template and item mappings onto a company — for a distributor location registered
/// after its siblings. Each section is offered only to a user who may manage that kind of mapping.
/// </summary>
[Area("Admin")]
[Authorize(PermissionConstants.Companies.Edit)]
public sealed class MappingCopyController(IMappingCopyService copyService, IAuthorizationService authorizationService)
    : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(int companyId, int? sourceCompanyId, CancellationToken cancellationToken)
    {
        var target = await copyService.GetCompanyAsync(companyId, cancellationToken);
        if (target is null) return NotFound();

        var model = new MappingCopyViewModel
        {
            TargetCompanyId = target.Id,
            TargetCompanyName = target.DisplayName,
            SourceCompanyId = sourceCompanyId,
            Sources = await copyService.ListSourceCompaniesAsync(companyId, cancellationToken),
            CanCopySellout = await CanAsync(PermissionConstants.DistributorTemplateMappings.Manage),
            CanCopyStock = await CanAsync(PermissionConstants.StockTemplateMappings.Manage),
            CanCopyItems = await CanAsync(PermissionConstants.ItemMappings.Manage)
        };
        if (sourceCompanyId is { } sourceId)
        {
            var preview = await copyService.PreviewAsync(sourceId, companyId, cancellationToken);
            if (preview.IsSuccess) model.Preview = preview.Data;
            else TempData["ErrorMessage"] = string.Join(" ", preview.Errors);
        }

        return View(model);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Copy(int companyId, int sourceCompanyId, List<int>? selloutMappingIds,
        List<int>? stockMappingIds, bool copyItemMappings, CancellationToken cancellationToken)
    {
        // Sections the user may not manage are dropped here, whatever the posted form says.
        var request = new MappingCopyRequestDto
        {
            SourceCompanyId = sourceCompanyId,
            TargetCompanyId = companyId,
            SelloutMappingIds = await CanAsync(PermissionConstants.DistributorTemplateMappings.Manage)
                ? selloutMappingIds ?? [] : [],
            StockMappingIds = await CanAsync(PermissionConstants.StockTemplateMappings.Manage)
                ? stockMappingIds ?? [] : [],
            CopyItemMappings = copyItemMappings && await CanAsync(PermissionConstants.ItemMappings.Manage)
        };
        var response = await copyService.CopyAsync(request, User.Identity?.Name ?? "unknown", cancellationToken);
        if (!response.IsSuccess)
        {
            TempData["ErrorMessage"] = string.Join(" ", response.Errors);
            return RedirectToAction(nameof(Index), new { companyId, sourceCompanyId });
        }

        var target = await copyService.GetCompanyAsync(companyId, cancellationToken);
        return View("Result", new MappingCopyResultViewModel
        {
            TargetCompanyId = companyId,
            TargetCompanyName = target?.DisplayName ?? string.Empty,
            Message = response.Message,
            Result = response.Data ?? new MappingCopyResultDto()
        });
    }

    private async Task<bool> CanAsync(string policy) =>
        (await authorizationService.AuthorizeAsync(User, policy)).Succeeded;
}
