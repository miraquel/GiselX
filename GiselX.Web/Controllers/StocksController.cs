using System.Data.SqlTypes;
using GiselX.Common.Constants;
using GiselX.Common.Helpers;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

public sealed class StocksController(
    IStockService stockService,
    IStockUploadService uploadService,
    UserManager<AppIdentityUser> userManager) : Controller
{
    [Authorize(PermissionConstants.Stocks.Index)]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var history = await stockService.ListCompletedUploadsAsync(cancellationToken);
        return View(new StockIndexViewModel { CompletedUploads = history.Data ?? [] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Stocks.Upload)]
    public async Task<IActionResult> PreflightUpload([FromForm(Name = "file")] IFormFile? fileInput,
        DateTime stockDate, CancellationToken cancellationToken)
    {
        if (fileInput is null || fileInput.Length == 0)
            return BadRequest(new ServiceResponse<StockUploadPreflightDto>
            {
                Errors = ["Please select a file to upload."], Message = "Please select a file to upload.",
                StatusCode = StatusCodes.Status400BadRequest
            });
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await using var stream = fileInput.OpenReadStream();
        var response = await uploadService.PreflightAsync(stream, fileInput.FileName, stockDate,
            user.CompanyId, cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpPost, ValidateAntiForgeryToken]
    [Authorize(PermissionConstants.Stocks.Upload)]
    public async Task<IActionResult> Upload([FromForm(Name = "file")] IFormFile? fileInput,
        DateTime stockDate, bool confirmed, CancellationToken cancellationToken)
    {
        if (fileInput is null || fileInput.Length == 0) return BadRequest("Please select a file to upload.");
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        await using var stream = fileInput.OpenReadStream();
        var response = await uploadService.UploadAsync(stream, fileInput.FileName, stockDate, user.CompanyId,
            confirmed, user.UserName ?? "unknown", cancellationToken);
        return StatusCode(response.StatusCode, response);
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Index)]
    public async Task<IActionResult> Result(long batchId, CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var response = await uploadService.GetResultAsync(batchId, user.CompanyId, cancellationToken);
        return response.Data is null ? NotFound() : View(response.Data);
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Download)]
    public async Task<IActionResult> DownloadBatch(long batchId, CancellationToken cancellationToken)
    {
        var response = await stockService.DownloadBatchAsync(batchId, User.IsInRole("Distributor"), cancellationToken);
        if (!response.IsSuccess || response.Data is null)
        {
            TempData["ErrorMessage"] = string.Join(" ", response.Errors);
            return RedirectToAction(nameof(Index));
        }
        var includeDistributor = User.IsInRole("Administrator");
        var columnOffset = includeDistributor ? 1 : 0;
        var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
        {
            if (row.RowNumber() == 1)
            {
                var headers = new[] { "Product ID", "Product Name", "Packaging", "Pcs/Ctn", "Netto", "Unit",
                    "Smallest Unit", "Qty per Smallest Unit", "Price Unit", "Saldo Awal", "Saldo Masuk PO",
                    "Saldo Akhir", "Batch No", "Expired Date", "Stock Date" };
                if (includeDistributor) row.Cell(1).Value = "Distributor";
                for (var index = 0; index < headers.Length; index++)
                    row.Cell(index + 1 + columnOffset).Value = headers[index];
            }
            else
            {
                if (includeDistributor) row.Cell(1).Value = dto.CompanyName;
                row.Cell(1 + columnOffset).Value = dto.ProductId; row.Cell(2 + columnOffset).Value = dto.ProductName;
                row.Cell(3 + columnOffset).Value = dto.ProductPackaging; row.Cell(4 + columnOffset).Value = dto.ProductPcsInCtn;
                row.Cell(5 + columnOffset).Value = dto.ProductNetto; row.Cell(6 + columnOffset).Value = dto.ProductUnit;
                row.Cell(7 + columnOffset).Value = dto.SmallestUnit; row.Cell(8 + columnOffset).Value = dto.SmallestUnitQty;
                row.Cell(9 + columnOffset).Value = dto.PriceUnit; row.Cell(10 + columnOffset).Value = dto.SaldoAwal;
                row.Cell(11 + columnOffset).Value = dto.SaldoMasukPO; row.Cell(12 + columnOffset).Value = dto.SaldoAkhir;
                row.Cell(13 + columnOffset).Value = dto.BatchNumber;
                row.Cell(14 + columnOffset).Value = dto.ExpiredDate == SqlDateTime.MinValue.Value ? null : dto.ExpiredDate.ToString("yyyy-MM-dd");
                row.Cell(15 + columnOffset).Value = dto.StockDate?.ToString("yyyy-MM-dd");
            }
        });
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"Stock_Batch_{batchId}.xlsx");
    }
}
