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

public class SalesTransactionsController : Controller
{
    private readonly ISalesTransactionService _salesTransactionService;
    private readonly UserManager<AppIdentityUser> _userManager;

    public SalesTransactionsController(ISalesTransactionService salesTransactionService, UserManager<AppIdentityUser> userManager)
    {
        _salesTransactionService = salesTransactionService;
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
            .SingleOrDefaultAsync(u => User.Identity != null && u.UserName == User.Identity.Name, cancellationToken: cancellationToken);

        if (user == null)
        {
            TempData["ErrorMessage"] = "Current User not found. aborting download.";
            return RedirectToAction(nameof(Index));
        }

        var response = await _salesTransactionService.SelectByCustPeriodAsync(user.CompanyId, year, month, cancellationToken);
        if (response is { IsSuccess: true, Data: not null })
        {
            var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
            {
                if (row.RowNumber() == 1)
                {
                    row.Cell(1).Value = "Customer ID";
                    row.Cell(2).Value = "Customer Name";
                    row.Cell(3).Value = "Customer Alias";
                    row.Cell(4).Value = "Customer Address";
                    row.Cell(5).Value = "Product ID";
                    row.Cell(6).Value = "Product Brand";
                    row.Cell(7).Value = "Product Name";
                    row.Cell(8).Value = "Product Packaging";
                    row.Cell(9).Value = "Pcs/Ctn";
                    row.Cell(10).Value = "Batch No";
                    row.Cell(11).Value = "Expired Date";
                    row.Cell(12).Value = "Invoice No";
                    row.Cell(13).Value = "Invoice Date";
                    row.Cell(14).Value = "Invoice Qty";
                    row.Cell(15).Value = "Unit";
                    row.Cell(16).Value = "Unit Price";
                    row.Cell(17).Value = "Gross Value";
                    row.Cell(18).Value = "Discount Value";
                    row.Cell(19).Value = "Discount %";
                    row.Cell(20).Value = "Net Value";
                    row.Cell(21).Value = "In Kg";
                    row.Cell(22).Value = "Geisa PO ID";
                    row.Cell(23).Value = "Ship Date";
                    row.Cell(24).Value = "Salesman";
                }
                else
                {
                    row.Cell(1).Value = dto.CustomerId;
                    row.Cell(2).Value = dto.CustomerName;
                    row.Cell(3).Value = dto.CustomerAlias;
                    row.Cell(4).Value = dto.CustomerAddress;
                    row.Cell(5).Value = dto.ProductId;
                    row.Cell(6).Value = dto.ProductBrand;
                    row.Cell(7).Value = dto.ProductName;
                    row.Cell(8).Value = dto.ProductPackaging;
                    row.Cell(9).Value = dto.ProductPcsInCtn;
                    row.Cell(10).Value = dto.BatchNumber;
                    row.Cell(11).Value = dto.ExpiredDate == SqlDateTime.MinValue.Value ? null : dto.ExpiredDate.ToString("yyyy-MM-dd");
                    row.Cell(12).Value = dto.InvoiceNo;
                    row.Cell(13).Value = dto.InvoiceDate == SqlDateTime.MinValue.Value ? null : dto.InvoiceDate.ToString("yyyy-MM-dd");
                    row.Cell(14).Value = dto.InvoiceQty;
                    row.Cell(15).Value = dto.InvoiceUnit;
                    row.Cell(16).Value = dto.UnitPrice;
                    row.Cell(17).Value = dto.GrossValue;
                    row.Cell(18).Value = dto.DiscountValue;
                    row.Cell(19).Value = dto.DiscountPct;
                    row.Cell(20).Value = dto.NetValue;
                    row.Cell(21).Value = dto.InKg;
                    row.Cell(22).Value = dto.GeisaPOId;
                    row.Cell(23).Value = dto.ShipDate == SqlDateTime.MinValue.Value ? null : dto.ShipDate.ToString("yyyy-MM-dd");
                    row.Cell(24).Value = dto.SalesmanNameGMK;
                }
            });

            var monthYear = new DateTime(year, month, 1);
            var fileName = $"SalesTransactions_{user.Company.Name}_{monthYear:MMMM_yyyy}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        TempData["ErrorMessage"] = response.Message ?? "Failed to download sales transactions.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Authorize(PermissionConstants.SalesTransactions.Upload)]
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

        var salesTransactions = new List<SalesTransactionDto>();

        try
        {
            using var stream = new MemoryStream();
            await fileInput.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            var uploadedAt = DateTime.Now;
            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            salesTransactions.AddRange(worksheet.RowsUsed()
                .Skip(1)
                .Select(row => new SalesTransactionDto
                {
                    CustomerId = row.Cell(1).TryGetValue(out string customerId) ? customerId : string.Empty,
                    CustomerName = row.Cell(2).TryGetValue(out string customerName) ? customerName : string.Empty,
                    CustomerAlias = row.Cell(3).TryGetValue(out string customerAlias) ? customerAlias : string.Empty,
                    CustomerAddress = row.Cell(4).TryGetValue(out string customerAddress) ? customerAddress : string.Empty,
                    ProductId = row.Cell(5).TryGetValue(out string productId) ? productId : string.Empty,
                    ProductBrand = row.Cell(6).TryGetValue(out string productBrand) ? productBrand : string.Empty,
                    ProductName = row.Cell(7).TryGetValue(out string productName) ? productName : string.Empty,
                    ProductPackaging = row.Cell(8).TryGetValue(out string productPackaging) ? productPackaging : string.Empty,
                    ProductPcsInCtn = row.Cell(9).TryGetValue(out int productPcsInCtn) ? productPcsInCtn : 0,
                    BatchNumber = row.Cell(10).TryGetValue(out string batchNumber) ? batchNumber : string.Empty,
                    ExpiredDate = row.Cell(11).TryGetValue(out DateTime expiredDate) ? expiredDate : SqlDateTime.MinValue.Value,
                    InvoiceNo = row.Cell(12).TryGetValue(out string invoiceNo) ? invoiceNo : string.Empty,
                    InvoiceDate = row.Cell(13).TryGetValue(out DateTime invoiceDate) ? invoiceDate : SqlDateTime.MinValue.Value,
                    InvoiceQty = row.Cell(14).TryGetValue(out decimal invoiceQty) ? invoiceQty : 0,
                    InvoiceUnit = row.Cell(15).TryGetValue(out string invoiceUnit) ? invoiceUnit : string.Empty,
                    UnitPrice = row.Cell(16).TryGetValue(out decimal unitPrice) ? unitPrice : 0,
                    GrossValue = row.Cell(17).TryGetValue(out decimal grossValue) ? grossValue : 0,
                    DiscountValue = row.Cell(18).TryGetValue(out decimal discountValue) ? discountValue : 0,
                    DiscountPct = row.Cell(19).TryGetValue(out decimal discountPct) ? discountPct : 0,
                    NetValue = row.Cell(20).TryGetValue(out decimal netValue) ? netValue : 0,
                    InKg = row.Cell(21).TryGetValue(out decimal inKg) ? inKg : 0,
                    GeisaPOId = row.Cell(22).TryGetValue(out string geisaPOId) ? geisaPOId : string.Empty,
                    ShipDate = row.Cell(23).TryGetValue(out DateTime shipDate) ? shipDate : SqlDateTime.MinValue.Value,
                    SalesmanNameGMK = row.Cell(24).TryGetValue(out string salesmanName) ? salesmanName : string.Empty,
                    CompanyId = user.CompanyId,
                    CreatedDate = uploadedAt
                }));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        var response = await _salesTransactionService.UploadAsync(salesTransactions, user.CompanyId, cancellationToken);
        if (response.IsSuccess)
        {
            TempData["SuccessMessage"] = response.Message;
        }
        else
        {
            TempData["ErrorMessage"] = response.Message?.Trim() ?? "Failed to upload sales transactions.";
        }

        return RedirectToAction(nameof(Index));
    }
}
