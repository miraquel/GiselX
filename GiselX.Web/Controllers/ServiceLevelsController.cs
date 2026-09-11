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
using Microsoft.EntityFrameworkCore;

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
    
    [HttpGet]
    [Authorize(PermissionConstants.ServiceLevels.Upload)]
    public IActionResult DownloadTemplate()
    {
        var stream = ExcelHelper.CreateExcelTemplate(headers =>
        {
            headers.Add("SO ID");
            headers.Add("SO Create Date");
            headers.Add("Lead Time Dlv");
            headers.Add("Lead Time Rct");
            headers.Add("Item ID");
            headers.Add("Item Name");
            headers.Add("SO Qty");
            headers.Add("Unit");
            headers.Add("Kg Per Unit");
            headers.Add("Dlv Date Request");
            headers.Add("Rct Date Request");
            headers.Add("DO Qty");
            headers.Add("DO Date");
            headers.Add("Receipt Date");
        });
        return File(stream, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "ServiceLevels_Template.xlsx");
    }

    [HttpGet]
    [Authorize(PermissionConstants.ServiceLevels.Download)]
    public async Task<IActionResult> DownloadSelectByCustPeriod(int year, int month, CancellationToken cancellationToken)
    {
        var user = await _userManager.Users.Include(u => u.Company).SingleOrDefaultAsync(u => User.Identity != null && u.UserName == User.Identity.Name, cancellationToken: cancellationToken);
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
            var includeDistributor = User.IsInRole("Administrator");
            var columnOffset = includeDistributor ? 1 : 0;
            var stream = ExcelHelper.ExportExcel(response.Data, (row, dto) =>
            {
                if (row.RowNumber() == 1)
                {
                    var headers = new[] { "SO ID", "SO Create Date", "Lead Time Dlv", "Lead Time Rct", "Item ID",
                        "Item Name", "SO Qty", "Unit", "Kg Per Unit", "Dlv Date Request", "Rct Date Request",
                        "DO Qty", "DO Date", "Receipt Date" };
                    if (includeDistributor) row.Cell(1).Value = "Distributor";
                    for (var index = 0; index < headers.Length; index++)
                        row.Cell(index + 1 + columnOffset).Value = headers[index];
                }
                else
                {
                    if (includeDistributor) row.Cell(1).Value = dto.CompanyName;
                    row.Cell(1 + columnOffset).Value = dto.SoId;
                    row.Cell(2 + columnOffset).Value = dto.SoCreateDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.SoCreateDate.ToString("yyyy-MM-dd");
                    row.Cell(3 + columnOffset).Value = dto.LeadTimeDlv;
                    row.Cell(4 + columnOffset).Value = dto.LeadTimeRct;
                    row.Cell(5 + columnOffset).Value = dto.ItemId;
                    row.Cell(6 + columnOffset).Value = dto.ItemName;
                    row.Cell(7 + columnOffset).Value = dto.SoQty;
                    row.Cell(8 + columnOffset).Value = dto.Unit;
                    row.Cell(9 + columnOffset).Value = dto.KgPerUnit;
                    row.Cell(10 + columnOffset).Value = dto.DlvDateRequest == SqlDateTime.MinValue.Value
                        ? null
                        : dto.DlvDateRequest.ToString("yyyy-MM-dd");
                    row.Cell(11 + columnOffset).Value = dto.RctDateRequest == SqlDateTime.MinValue.Value
                        ? null
                        : dto.RctDateRequest.ToString("yyyy-MM-dd");
                    row.Cell(12 + columnOffset).Value = dto.DoQty;
                    row.Cell(13 + columnOffset).Value = dto.DoDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.DoDate.ToString("yyyy-MM-dd");
                    row.Cell(14 + columnOffset).Value = dto.ReceiptDate == SqlDateTime.MinValue.Value
                        ? null
                        : dto.ReceiptDate.ToString("yyyy-MM-dd");
                }
            });

            // format the file name as ServiceLevels_CompanyId_Month_Year.xlsx
            var monthYear = new DateTime(year, month, 1);
            var companyLabel = includeDistributor ? "All_Distributors" : user.Company.Name;
            var fileName = $"ServiceLevels_{companyLabel}_{monthYear:MMMM_yyyy}.xlsx";
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

        var response = await _serviceLevelService.UploadAsync(serviceLevels, user.CompanyId, cancellationToken);
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
