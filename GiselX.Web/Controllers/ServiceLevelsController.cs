using System.Data.SqlTypes;
using System.Security.Claims;
using GiselX.Common.Constants;
using GiselX.Common.Helpers;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

public class ServiceLevelsController : Controller
{
    private readonly IServiceLevelService _serviceLevelService;
    private readonly UserManager<AppIdentityUser> _userManager;

    public ServiceLevelsController(IServiceLevelService serviceLevelService, UserManager<AppIdentityUser> userManager)
    {
        _serviceLevelService = serviceLevelService;
        _userManager = userManager;
    }

    // GET: ServiceLevelController
    [Authorize(PermissionConstants.ServiceLevels.Index)]
    public IActionResult Index()
    {
        return View();
    }
    
    // Download SelectByCustPeriod
    [HttpGet]
    public async Task<IActionResult> DownloadSelectByCustPeriod(int year, int month, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            TempData["ErrorMessage"] = "Current User not found. aborting download.";
            return RedirectToAction(nameof(Index));
        }

        if (user.UserName == null)
        {
            TempData["ErrorMessage"] = "Current UserName not found. aborting download.";
            return RedirectToAction(nameof(Index));
        }
        
        var response = await _serviceLevelService.SelectByCustPeriodAsync(user.UserName, year, month, cancellationToken);
        if (response is { IsSuccess: true, Data: not null })
        {
            var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
            {
                if (row.RowNumber() == 1)
                {
                    row.Cell(1).Value = "SO ID";
                    row.Cell(2).Value = "SO Create Date";
                    row.Cell(3).Value = "Lead Time Dlv";
                    row.Cell(4).Value = "Lead Time Rct";
                    row.Cell(5).Value = "Item ID";
                    row.Cell(6).Value = "Item Name";
                    row.Cell(7).Value = "SO Qty";
                    row.Cell(8).Value = "Unit";
                    row.Cell(9).Value = "Kg Per Unit";
                    row.Cell(10).Value = "Dlv Date Request";
                    row.Cell(11).Value = "Rct Date Request";
                    row.Cell(12).Value = "DO Qty";
                    row.Cell(13).Value = "DO Date";
                    row.Cell(14).Value = "Receipt Date";
                }
                else
                {
                    row.Cell(1).Value = dto.SoId;
                    row.Cell(2).Value = dto.SoCreateDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.SoCreateDate.ToString("yyyy-MM-dd");
                    row.Cell(3).Value = dto.LeadTimeDlv;
                    row.Cell(4).Value = dto.LeadTimeRct;
                    row.Cell(5).Value = dto.ItemId;
                    row.Cell(6).Value = dto.ItemName;
                    row.Cell(7).Value = dto.SoQty;
                    row.Cell(8).Value = dto.Unit;
                    row.Cell(9).Value = dto.KgPerUnit;
                    row.Cell(10).Value = dto.DlvDateRequest == SqlDateTime.MinValue.Value
                        ? null
                        : dto.DlvDateRequest.ToString("yyyy-MM-dd");
                    row.Cell(11).Value = dto.RctDateRequest == SqlDateTime.MinValue.Value
                        ? null
                        : dto.RctDateRequest.ToString("yyyy-MM-dd");
                    row.Cell(12).Value = dto.DoQty;
                    row.Cell(13).Value = dto.DoDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.DoDate.ToString("yyyy-MM-dd");
                    row.Cell(14).Value = dto.ReceiptDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.ReceiptDate.ToString("yyyy-MM-dd");
                }
            });

            var fileName = $"ServiceLevels_{user.CompanyId}_{month:MMMM}_{year:yyyy}.xlsx";
            return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        TempData["ErrorMessage"] = response.Message ?? "Failed to download service levels.";
        return RedirectToAction(nameof(Index));
    }
        
    [HttpPost]
    [Authorize(PermissionConstants.ServiceLevels.Upload)]
    public async Task<IActionResult> Upload([FromForm(Name = "file")] IFormFile fileInput, CancellationToken cancellationToken)
    {
        // convert the uploaded Excel file to a list of ServiceLevelDto using ClosedXML
        if (fileInput.Length == 0)
        {
            TempData["ErrorMessage"] = "Please select a file to upload.";
            return RedirectToAction(nameof(Index));
        }
            
        var serviceLevels = new List<ServiceLevelDto>();
        var user = await _userManager.GetUserAsync(User);
        
        if (user == null)
        {
            TempData["ErrorMessage"] = "Current User not found. aborting upload.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var stream = new MemoryStream();
            await fileInput.CopyToAsync(stream, cancellationToken);
            stream.Position = 0;

            using var workbook = new ClosedXML.Excel.XLWorkbook(stream);
            var worksheet = workbook.Worksheet(1);
            serviceLevels.AddRange(worksheet.RowsUsed()
                .Skip(1)
                .Select(row => new ServiceLevelDto
                {
                    SoId = row.Cell(1).TryGetValue(out string soId) ? soId : string.Empty,
                    SoCreateDate = row.Cell(2).TryGetValue(out DateTime soCreateDate) ? soCreateDate : SqlDateTime.MinValue.Value,
                    LeadTimeDlv = row.Cell(3).TryGetValue(out int leadTimeDlv) ? leadTimeDlv : 0,
                    LeadTimeRct = row.Cell(4).TryGetValue(out int leadTimeRct) ? leadTimeRct : 0,
                    ItemId = row.Cell(5).TryGetValue(out string itemId) ? itemId : string.Empty,
                    ItemName = row.Cell(6).TryGetValue(out string itemName) ? itemName : string.Empty,
                    SoQty = row.Cell(7).TryGetValue(out decimal soQty) ? soQty : 0,
                    Unit = row.Cell(8).TryGetValue(out string unit) ? unit : string.Empty,
                    KgPerUnit = row.Cell(9).TryGetValue(out decimal kgPerUnit) ? kgPerUnit : 0,
                    DlvDateRequest = row.Cell(10).TryGetValue(out DateTime dlvDate) ? dlvDate : SqlDateTime.MinValue.Value,
                    RctDateRequest = row.Cell(11).TryGetValue(out DateTime rctDate) ? rctDate : SqlDateTime.MinValue.Value,
                    DoQty = row.Cell(12).TryGetValue(out decimal doQty) ? doQty : 0,
                    DoDate = row.Cell(13).TryGetValue(out DateTime doDate) ? doDate : SqlDateTime.MinValue.Value,
                    ReceiptDate = row.Cell(14).TryGetValue(out DateTime receiptDate) ? receiptDate : SqlDateTime.MinValue.Value,
                    CompanyId = user.CompanyId,
                    CreatedBy = user.UserName ?? user.Email ?? "Unknown",
                    CreatedDate = DateTime.Now
                }));
        }
        catch (Exception ex)
        {
            TempData["ErrorMessage"] = $"Error processing file: {ex.Message}";
            return RedirectToAction(nameof(Index));
        }

        var response = await _serviceLevelService.UploadAsync(serviceLevels, cancellationToken);
        if (response.IsSuccess)
        {
            TempData["SuccessMessage"] = response.Message;
        }
        else
        {
            TempData["ErrorMessage"] = response.Message?.Trim() ?? "Failed to upload transaction distributions.";
        }

        return RedirectToAction(nameof(Index));
    }
}