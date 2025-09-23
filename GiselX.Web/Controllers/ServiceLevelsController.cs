using System.Data.SqlTypes;
using System.Security.Claims;
using GiselX.Common.Constants;
using GiselX.Service.Dto;
using GiselX.Service.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Web.Controllers;

public class ServiceLevelsController : Controller
{
    private readonly IServiceLevelService _serviceLevelService;

    public ServiceLevelsController(IServiceLevelService serviceLevelService)
    {
        _serviceLevelService = serviceLevelService;
    }

    // GET: ServiceLevelController
    [Authorize(PermissionConstants.ServiceLevels.Index)]
    public IActionResult Index()
    {
        return View();
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
                    CreatedBy = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ?? "System",
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