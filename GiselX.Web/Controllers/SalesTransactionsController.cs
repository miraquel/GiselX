using System.Data.SqlTypes;
using GiselX.Common.Constants;
using GiselX.Common.Helpers;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Web.Controllers;

public class SalesTransactionsController : Controller
{
    private readonly ISalesTransactionService _salesTransactionService;
    private readonly ISelloutUploadService _selloutUploadService;
    private readonly UserManager<AppIdentityUser> _userManager;

    public SalesTransactionsController(ISalesTransactionService salesTransactionService, ISelloutUploadService selloutUploadService, UserManager<AppIdentityUser> userManager)
    {
        _salesTransactionService = salesTransactionService;
        _selloutUploadService = selloutUploadService;
        _userManager = userManager;
    }

    [Authorize(PermissionConstants.SalesTransactions.Index)]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Upload)]
    public IActionResult DownloadTemplate()
    {
        var stream = ExcelHelper.CreateExcelTemplate(headers =>
        {
            headers.Add("Customer ID");
            headers.Add("Customer Name");
            headers.Add("Customer Alias");
            headers.Add("Customer Address");
            headers.Add("Product ID");
            headers.Add("Product Brand");
            headers.Add("Product Name");
            headers.Add("Product Packaging");
            headers.Add("Pcs/Ctn");
            headers.Add("Batch No");
            headers.Add("Expired Date");
            headers.Add("Invoice No");
            headers.Add("Invoice Date");
            headers.Add("Invoice Qty");
            headers.Add("Unit");
            headers.Add("Unit Price");
            headers.Add("Gross Value");
            headers.Add("Discount Value");
            headers.Add("Discount %");
            headers.Add("Net Value");
            headers.Add("In Kg");
            headers.Add("Geisa PO ID");
            headers.Add("Ship Date");
            headers.Add("Salesman");
        });
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "SalesTransactions_Template.xlsx");
    }

    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Download)]
    public async Task<IActionResult> DownloadSelectByCustPeriod(int year, int month, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Include(u => u.Company)
            .SingleOrDefaultAsync(u => User.Identity != null && u.UserName == User.Identity.Name,
                cancellationToken: cancellationToken);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Sesi login tidak ditemukan. Silakan login ulang.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _salesTransactionService.SelectByCustPeriodAsync(user.CompanyId, year, month,
            User.SeesDistributorItemIdentity(), cancellationToken);
        if (response is { IsSuccess: true, Data: not null })
        {
            var includeDistributor = User.IsInRole("Administrator");
            var columnOffset = includeDistributor ? 1 : 0;
            var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
            {
                if (row.RowNumber() == 1)
                {
                    var headers = new[] { "Customer ID", "Customer Name", "Customer Alias", "Customer Address",
                        "Product ID", "Product Brand", "Product Name", "Product Packaging", "Pcs/Ctn", "Batch No",
                        "Expired Date", "Invoice No", "Invoice Date", "Invoice Qty", "Unit", "Unit Price",
                        "Gross Value", "Discount Value", "Discount %", "Net Value", "In Kg", "Geisa PO ID",
                        "Ship Date", "Salesman" };
                    if (includeDistributor) row.Cell(1).Value = "Distributor";
                    for (var index = 0; index < headers.Length; index++)
                        row.Cell(index + 1 + columnOffset).Value = headers[index];
                }
                else
                {
                    if (includeDistributor) row.Cell(1).Value = dto.CompanyName;
                    row.Cell(1 + columnOffset).Value = dto.CustomerId;
                    row.Cell(2 + columnOffset).Value = dto.CustomerName;
                    row.Cell(3 + columnOffset).Value = dto.CustomerAlias;
                    row.Cell(4 + columnOffset).Value = dto.CustomerAddress;
                    row.Cell(5 + columnOffset).Value = dto.ProductId;
                    row.Cell(6 + columnOffset).Value = dto.ProductBrand;
                    row.Cell(7 + columnOffset).Value = dto.ProductName;
                    row.Cell(8 + columnOffset).Value = dto.ProductPackaging;
                    row.Cell(9 + columnOffset).Value = dto.ProductPcsInCtn;
                    row.Cell(10 + columnOffset).Value = dto.BatchNumber;
                    row.Cell(11 + columnOffset).Value = dto.ExpiredDate == SqlDateTime.MinValue.Value ? null : dto.ExpiredDate.ToString("yyyy-MM-dd");
                    row.Cell(12 + columnOffset).Value = dto.InvoiceNo;
                    row.Cell(13 + columnOffset).Value = dto.InvoiceDate == SqlDateTime.MinValue.Value ? null : dto.InvoiceDate.ToString("yyyy-MM-dd");
                    row.Cell(14 + columnOffset).Value = dto.InvoiceQty;
                    row.Cell(15 + columnOffset).Value = dto.InvoiceUnit;
                    row.Cell(16 + columnOffset).Value = dto.UnitPrice;
                    row.Cell(17 + columnOffset).Value = dto.GrossValue;
                    row.Cell(18 + columnOffset).Value = dto.DiscountValue;
                    row.Cell(19 + columnOffset).Value = dto.DiscountPct;
                    row.Cell(20 + columnOffset).Value = dto.NetValue;
                    row.Cell(21 + columnOffset).Value = dto.InKg;
                    row.Cell(22 + columnOffset).Value = dto.GeisaPOId;
                    row.Cell(23 + columnOffset).Value = dto.ShipDate == SqlDateTime.MinValue.Value ? null : dto.ShipDate.ToString("yyyy-MM-dd");
                    row.Cell(24 + columnOffset).Value = dto.SalesmanNameGMK;
                }
            });

            var monthYear = new DateTime(year, month, 1);
            var companyLabel = includeDistributor ? "All_Distributors" : user.Company.Name;
            var fileName = $"SalesTransactions_{companyLabel}_{monthYear:MMMM_yyyy}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        TempData["ErrorMessage"] = response.Message ?? "Gagal mengunduh data sales.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(PermissionConstants.SalesTransactions.Upload)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PreflightUpload(
        [FromForm(Name = "file")] IFormFile? fileInput,
        CancellationToken cancellationToken)
    {
        if (fileInput is null || fileInput.Length == 0)
        {
            return BadRequest(new ServiceResponse<SelloutUploadPreflightDto>
            {
                Errors = ["Pilih file yang akan diupload."],
                Message = "Pilih file yang akan diupload.",
                StatusCode = StatusCodes.Status400BadRequest
            });
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        await using var stream = fileInput.OpenReadStream();
        var response = await _selloutUploadService.PreflightAsync(
            stream, fileInput.FileName, user.CompanyId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost]
    [Authorize(PermissionConstants.SalesTransactions.Upload)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upload([FromForm(Name = "file")] IFormFile fileInput, bool multiWeekConfirmed,
        string? confirmedSha256, CancellationToken cancellationToken)
    {
        if (fileInput.Length == 0)
        {
            TempData["ErrorMessage"] = "Pilih file yang akan diupload.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Sesi login tidak ditemukan. Silakan login ulang.";
            return RedirectToAction(nameof(Index));
        }

        await using var stream = fileInput.OpenReadStream();
        var response = await _selloutUploadService.UploadAsync(stream, fileInput.FileName, user.CompanyId,
            multiWeekConfirmed, user.UserName ?? "unknown", confirmedSha256, cancellationToken);
        if (response.Data?.GroupId > 0) return RedirectToAction(nameof(UploadResult), new { groupId = response.Data.GroupId });
        TempData["ErrorMessage"] = response.Message ?? "Gagal mengupload data Sellout.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Hands back the workbook as it was uploaded. Scoped to the signed-in user's own company (any company for
    /// an Administrator), so the link in a forwarded notification email is useless to anyone outside it.
    /// </summary>
    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Index)]
    public async Task<IActionResult> DownloadSource(long groupId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var response = await _selloutUploadService.DownloadSourceAsync(groupId, UploadScope(user), cancellationToken);
        if (!response.IsSuccess || response.Data is null)
        {
            TempData["ErrorMessage"] = string.Join(" ", response.Errors);
            return RedirectToAction(nameof(Index));
        }

        return File(response.Data.Content, response.Data.ContentType, response.Data.FileName);
    }

    [HttpGet]
    [Authorize(PermissionConstants.SalesTransactions.Upload)]
    public async Task<IActionResult> UploadResult(long? groupId, long? batchId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var response = await _selloutUploadService.GetUploadResultAsync(groupId, batchId, UploadScope(user), cancellationToken);
        return response.Data is null ? NotFound() : View(response.Data);
    }

    /// <summary>
    /// The company an upload result or archived workbook is looked up in. Administrators are PT. GMK
    /// staff whose own CompanyId is not the distributor's, and the internal notification mail sends them
    /// these same links — scoping them to their own company would 404 every one.
    /// </summary>
    private int UploadScope(AppIdentityUser user) => User.IsInRole("Administrator") ? 0 : user.CompanyId;
}
