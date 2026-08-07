using System.Data.SqlTypes;
using GiselX.Common.Constants;
using GiselX.Common.Helpers;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace GiselX.Web.Controllers;

public class StocksController : Controller
{
    private readonly IStockService _stockService;
    private readonly UserManager<AppIdentityUser> _userManager;

    public StocksController(IStockService stockService, UserManager<AppIdentityUser> userManager)
    {
        _stockService = stockService;
        _userManager = userManager;
    }

    [Authorize(PermissionConstants.Stocks.Index)]
    public IActionResult Index()
    {
        return View();
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Upload)]
    public IActionResult DownloadTemplate()
    {
        var stream = ExcelHelper.CreateExcelTemplate(headers =>
        {
            headers.Add("Product ID");
            headers.Add("Product Name");
            headers.Add("Packaging");
            headers.Add("Pcs/Ctn");
            headers.Add("Netto");
            headers.Add("Unit");
            headers.Add("Saldo Awal");
            headers.Add("Saldo Masuk PO");
            headers.Add("Saldo Akhir");
            headers.Add("Batch No");
            headers.Add("Expired Date");
        });
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "Stocks_Template.xlsx");
    }

    [HttpGet]
    [Authorize(PermissionConstants.Stocks.Download)]
    public async Task<IActionResult> DownloadSelectByCustPeriod(int year, int month, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Include(u => u.Company)
            .SingleOrDefaultAsync(u => User.Identity != null && u.UserName == User.Identity.Name, cancellationToken: cancellationToken);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Current User not found. aborting download.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _stockService.SelectByCustPeriodAsync(user.CompanyId, year, month, cancellationToken);
        if (response is { IsSuccess: true, Data: not null })
        {
            var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
            {
                if (row.RowNumber() == 1)
                {
                    row.Cell(1).Value = "Product ID";
                    row.Cell(2).Value = "Product Name";
                    row.Cell(3).Value = "Packaging";
                    row.Cell(4).Value = "Pcs/Ctn";
                    row.Cell(5).Value = "Netto";
                    row.Cell(6).Value = "Unit";
                    row.Cell(7).Value = "Saldo Awal";
                    row.Cell(8).Value = "Saldo Masuk PO";
                    row.Cell(9).Value = "Saldo Akhir";
                    row.Cell(10).Value = "Batch No";
                    row.Cell(11).Value = "Expired Date";
                }
                else
                {
                    row.Cell(1).Value = dto.ProductId;
                    row.Cell(2).Value = dto.ProductName;
                    row.Cell(3).Value = dto.ProductPackaging;
                    row.Cell(4).Value = dto.ProductPcsInCtn;
                    row.Cell(5).Value = dto.ProductNetto;
                    row.Cell(6).Value = dto.ProductUnit;
                    row.Cell(7).Value = dto.SaldoAwal;
                    row.Cell(8).Value = dto.SaldoMasukPO;
                    row.Cell(9).Value = dto.SaldoAkhir;
                    row.Cell(10).Value = dto.BatchNumber;
                    row.Cell(11).Value = dto.ExpiredDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.ExpiredDate.ToString("yyyy-MM-dd");
                }
            });

            var monthYear = new DateTime(year, month, 1);
            var fileName = $"Stocks_{user.Company.Name}_{monthYear:MMMM_yyyy}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        TempData["ErrorMessage"] = response.Message ?? "Failed to download stocks.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(PermissionConstants.Stocks.Upload)]
    public async Task<IActionResult> Upload([FromForm(Name = "file")] IFormFile fileInput, CancellationToken cancellationToken)
    {
        if (fileInput.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToAction(nameof(Index));
        }

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Current User not found. aborting upload.";
            return RedirectToAction(nameof(Index));
        }

        var stocks = new List<StockDto>();

        try
        {
            using var stream = new MemoryStream();
            await fileInput.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            stocks.AddRange(worksheet.RowsUsed()
                .Skip(1)
                .Select(row => new StockDto
                {
                    ProductId = row.Cell(1).TryGetValue(out string productId) ? productId : string.Empty,
                    ProductName = row.Cell(2).TryGetValue(out string productName) ? productName : string.Empty,
                    ProductPackaging = row.Cell(3).TryGetValue(out string productPackaging) ? productPackaging : string.Empty,
                    ProductPcsInCtn = row.Cell(4).TryGetValue(out int productPcsInCtn) ? productPcsInCtn : 0,
                    ProductNetto = row.Cell(5).TryGetValue(out decimal productNetto) ? productNetto : 0,
                    ProductUnit = row.Cell(6).TryGetValue(out string productUnit) ? productUnit : string.Empty,
                    SaldoAwal = row.Cell(7).TryGetValue(out decimal saldoAwal) ? saldoAwal : 0,
                    SaldoMasukPO = row.Cell(8).TryGetValue(out decimal saldoMasukPO) ? saldoMasukPO : 0,
                    SaldoAkhir = row.Cell(9).TryGetValue(out decimal saldoAkhir) ? saldoAkhir : 0,
                    BatchNumber = row.Cell(10).TryGetValue(out string batchNumber) ? batchNumber : string.Empty,
                    ExpiredDate = row.Cell(11).TryGetValue(out DateTime expiredDate) ? expiredDate : SqlDateTime.MinValue.Value,
                    CompanyId = user.CompanyId,
                    CreatedDate = DateTime.Now
                }));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        var response = await _stockService.UploadAsync(stocks, user.CompanyId, cancellationToken);
        if (response.IsSuccess)
        {
            TempData["SuccessMessage"] = response.Message;
        }
        else
        {
            TempData["ErrorMessage"] = response.Message?.Trim() ?? "Failed to upload stocks.";
        }

        return RedirectToAction(nameof(Index));
    }
}
