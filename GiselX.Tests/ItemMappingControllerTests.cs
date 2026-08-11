using System.Security.Claims;
using ClosedXML.Excel;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GiselX.Tests;

public sealed class ItemMappingControllerTests
{
    [Fact]
    public async Task Index_routes_company_to_mapping_service()
    {
        var service = new CapturingItemMappingService
        {
            Mappings = [new ItemMappingDto { Id = 5, CompanyId = 7, DistributorItemId = "D-1" }]
        };
        var controller = CreateController(service);

        var result = await controller.Index(7, null, null, null, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ItemMappingListViewModel>(view.Model);
        Assert.Equal(7, service.ListedCompanyId);
        Assert.Single(model.Mappings);
    }

    [Fact]
    public async Task Edit_returns_not_found_when_mapping_does_not_belong_to_company()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var result = await controller.Edit(9, 7, CancellationToken.None);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Activate_forwards_server_owned_true_state()
    {
        var service = new CapturingItemMappingService();
        var controller = CreateController(service);

        var result = await controller.Activate(9, 7, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((9, 7, true, "admin"), service.ActiveChange);
    }

    [Fact]
    public async Task Deactivate_forwards_server_owned_false_state()
    {
        var service = new CapturingItemMappingService();
        var controller = CreateController(service);

        var result = await controller.Deactivate(9, 7, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal((9, 7, false, "admin"), service.ActiveChange);
    }

    [Fact]
    public async Task Save_forwards_the_four_required_identity_fields()
    {
        var service = new CapturingItemMappingService();
        var controller = CreateController(service);
        var model = new ItemMappingEditViewModel
        {
            CompanyId = 7,
            GmkItemId = "4100039",
            GmkItemName = "GMK product",
            DistributorItemId = "P0003",
            DistributorItemName = "Distributor product"
        };

        var result = await controller.Save(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(7, service.Saved!.CompanyId);
        Assert.Equal("4100039", service.Saved.GmkItemId);
        Assert.Equal("Distributor product", service.Saved.DistributorItemName);
    }

    [Fact]
    public void DownloadTemplate_contains_the_four_required_headers()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var file = Assert.IsType<FileContentResult>(controller.DownloadTemplate(7));

        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        Assert.Equal("GMK Item ID", workbook.Worksheet(1).Cell(1, 1).GetString());
        Assert.Equal("Distributor Item Name", workbook.Worksheet(1).Cell(1, 4).GetString());
    }

    [Fact]
    public async Task PreflightImport_passes_selected_file_and_company_to_import_service()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "items.xlsx");

        var result = await controller.PreflightImport(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(7, import.PreflightCompanyId);
        Assert.Equal(new byte[] { 1, 2, 3 }, import.PreflightWorkbook);
    }

    [Fact]
    public async Task Import_confirms_server_owned_true_state_after_browser_confirmation()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream([4, 5]);
        var file = new FormFile(content, 0, content.Length, "file", "items.xlsx");

        var result = await controller.Import(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal((7, true, "admin"), import.ImportRequest);
        Assert.Equal(new byte[] { 4, 5 }, import.ImportWorkbook);
    }

    private static ItemMappingsController CreateController(CapturingItemMappingService service,
        CapturingImportService? importService = null)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin")], "TestAuthentication"))
        };
        return new ItemMappingsController(service, importService ?? new CapturingImportService())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };
    }

    private sealed class CapturingItemMappingService : IItemMappingService
    {
        public IReadOnlyList<ItemMappingDto> Mappings { get; init; } = [];
        public int? ListedCompanyId { get; private set; }
        public SaveItemMappingDto? Saved { get; private set; }
        public (int Id, int CompanyId, bool IsActive, string Username)? ActiveChange { get; private set; }

        public Task<ServiceResponse<IReadOnlyList<ItemMappingDto>>> ListAsync(int companyId, CancellationToken token)
        {
            ListedCompanyId = companyId;
            return Task.FromResult(new ServiceResponse<IReadOnlyList<ItemMappingDto>> { Data = Mappings });
        }

        public Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token) =>
            Task.FromResult(new ServiceResponse<ItemMappingDto?> { Data = null, StatusCode = StatusCodes.Status404NotFound });

        public Task<ServiceResponse<int>> SaveAsync(SaveItemMappingDto dto, string username, CancellationToken token)
        {
            Saved = dto;
            return Task.FromResult(new ServiceResponse<int> { Data = 1, Message = "Item mapping saved successfully." });
        }

        public Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool active, string username,
            CancellationToken token)
        {
            ActiveChange = (id, companyId, active, username);
            return Task.FromResult(new ServiceResponse { Message = active ? "Mapping activated." : "Mapping deactivated." });
        }

        public Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken token) =>
            Task.FromResult(new ServiceResponse { Message = "Item mapping deleted successfully." });
    }

    private sealed class CapturingImportService : IItemMappingImportService
    {
        public int? PreflightCompanyId { get; private set; }
        public byte[]? PreflightWorkbook { get; private set; }
        public (int CompanyId, bool Confirmed, string Username)? ImportRequest { get; private set; }
        public byte[]? ImportWorkbook { get; private set; }

        public Task<ServiceResponse<ItemMappingImportPreviewDto>> PreflightAsync(byte[] workbook, int companyId,
            CancellationToken cancellationToken)
        {
            PreflightCompanyId = companyId;
            PreflightWorkbook = workbook;
            return Task.FromResult(new ServiceResponse<ItemMappingImportPreviewDto>
            {
                Data = new ItemMappingImportPreviewDto()
            });
        }

        public Task<ServiceResponse<ItemMappingImportResultDto>> ImportAsync(byte[] workbook, int companyId,
            bool confirmed, string username, CancellationToken cancellationToken)
        {
            ImportWorkbook = workbook;
            ImportRequest = (companyId, confirmed, username);
            return Task.FromResult(new ServiceResponse<ItemMappingImportResultDto>
            {
                Data = new ItemMappingImportResultDto()
            });
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
