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
using NPOI.HSSF.UserModel;

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
    public async Task Edit_loads_the_saved_inactive_status()
    {
        var service = new CapturingItemMappingService
        {
            Mappings = [new ItemMappingDto { Id = 9, CompanyId = 7, IsActive = false }]
        };
        var controller = CreateController(service);

        var result = await controller.Edit(9, 7, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ItemMappingEditViewModel>(view.Model);
        Assert.False(model.IsActive);
    }

    [Fact]
    public async Task Index_loads_unmapped_distributor_items_for_the_same_company()
    {
        var service = new CapturingItemMappingService
        {
            Unmapped = new UnmappedDistributorItemListDto
            {
                ItemMappingEnabled = true,
                Items = [new UnmappedDistributorItemDto { DistributorItemId = "D-9", DistributorItemName = "Biskuit" }]
            }
        };
        var controller = CreateController(service);

        var result = await controller.Index(7, null, null, null, CancellationToken.None);

        var model = Assert.IsType<ItemMappingListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7, service.ListedUnmappedCompanyId);
        Assert.True(model.ItemMappingEnabled);
        Assert.Equal("D-9", Assert.Single(model.UnmappedItems).DistributorItemId);
    }

    [Fact]
    public async Task Index_hides_the_unmapped_tab_when_item_mapping_is_disabled()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var result = await controller.Index(7, null, null, null, CancellationToken.None);

        var model = Assert.IsType<ItemMappingListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.False(model.ItemMappingEnabled);
        Assert.Empty(model.UnmappedItems);
    }

    [Fact]
    public async Task Index_applies_the_search_term_to_unmapped_distributor_items()
    {
        var service = new CapturingItemMappingService
        {
            Unmapped = new UnmappedDistributorItemListDto
            {
                ItemMappingEnabled = true,
                Items =
                [
                    new UnmappedDistributorItemDto { DistributorItemId = "D-9", DistributorItemName = "Biskuit" },
                    new UnmappedDistributorItemDto { DistributorItemId = "D-10", DistributorItemName = "Susu" }
                ]
            }
        };
        var controller = CreateController(service);

        var result = await controller.Index(7, "susu", null, null, CancellationToken.None);

        var model = Assert.IsType<ItemMappingListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal("D-10", Assert.Single(model.UnmappedItems).DistributorItemId);
    }

    [Fact]
    public void Create_prefills_the_distributor_identity_handed_over_from_the_unmapped_tab()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var result = controller.Create(7, "D-9", "Biskuit Coklat");

        var model = Assert.IsType<ItemMappingEditViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(7, model.CompanyId);
        Assert.Equal("D-9", model.DistributorItemId);
        Assert.Equal("Biskuit Coklat", model.DistributorItemName);
        Assert.Empty(model.GmkItemId);
        Assert.Empty(model.GmkItemName);
        Assert.True(model.IsActive);
    }

    [Fact]
    public void Create_defaults_status_to_active()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var result = controller.Create(7);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<ItemMappingEditViewModel>(view.Model);
        Assert.True(model.IsActive);
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
    public async Task Save_forwards_the_identity_fields_and_status()
    {
        var service = new CapturingItemMappingService();
        var controller = CreateController(service);
        var model = new ItemMappingEditViewModel
        {
            CompanyId = 7,
            GmkItemId = "4100039",
            GmkItemName = "GMK product",
            DistributorItemId = "P0003",
            DistributorItemName = "Distributor product",
            IsActive = false
        };

        var result = await controller.Save(model, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(7, service.Saved!.CompanyId);
        Assert.Equal("4100039", service.Saved.GmkItemId);
        Assert.Equal("Distributor product", service.Saved.DistributorItemName);
        Assert.False(service.Saved.IsActive);
    }

    [Fact]
    public void DownloadTemplate_contains_the_required_headers_and_status()
    {
        var controller = CreateController(new CapturingItemMappingService());

        var file = Assert.IsType<FileContentResult>(controller.DownloadTemplate(7));

        using var workbook = new XLWorkbook(new MemoryStream(file.FileContents));
        Assert.Equal("GMK Item ID", workbook.Worksheet(1).Cell(1, 1).GetString());
        Assert.Equal("Distributor Item Name", workbook.Worksheet(1).Cell(1, 4).GetString());
        Assert.Equal("Status", workbook.Worksheet(1).Cell(1, 5).GetString());
        Assert.Equal("Active", workbook.Worksheet("Instructions").Cell(2, 2).GetString());
        Assert.Equal("Inactive", workbook.Worksheet("Instructions").Cell(3, 2).GetString());
    }

    [Fact]
    public async Task PreflightImport_passes_selected_file_and_company_to_import_service()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        var workbook = ValidWorkbook();
        await using var content = new MemoryStream(workbook);
        var file = new FormFile(content, 0, content.Length, "file", "items.xlsx");

        var result = await controller.PreflightImport(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(7, import.PreflightCompanyId);
        Assert.Equal(workbook, import.PreflightWorkbook);
    }

    [Fact]
    public async Task PreflightImport_rejects_a_file_that_is_not_xlsx()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream([1, 2, 3]);
        var file = new FormFile(content, 0, content.Length, "file", "items.csv");

        var result = await controller.PreflightImport(7, file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ServiceResponse>(badRequest.Value);
        Assert.Contains(".xlsx", Assert.Single(response.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Null(import.PreflightWorkbook);
    }

    [Fact]
    public async Task Import_confirms_server_owned_true_state_after_browser_confirmation()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        var workbook = ValidWorkbook();
        await using var content = new MemoryStream(workbook);
        var file = new FormFile(content, 0, content.Length, "file", "items.xlsx");

        var result = await controller.Import(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal((7, true, "admin"), import.ImportRequest);
        Assert.Equal(workbook, import.ImportWorkbook);
    }

    [Fact]
    public async Task Import_rejects_a_corrupt_xlsx_before_calling_import_service()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream([4, 5]);
        var file = new FormFile(content, 0, content.Length, "file", "items.xlsx");

        var result = await controller.Import(7, file, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var response = Assert.IsType<ServiceResponse>(badRequest.Value);
        Assert.Contains("valid .xlsx", Assert.Single(response.Errors), StringComparison.OrdinalIgnoreCase);
        Assert.Null(import.ImportWorkbook);
    }

    [Fact]
    public async Task PreflightImport_converts_a_legacy_xls_upload_before_reaching_the_import_service()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream(LegacyWorkbook());
        var file = new FormFile(content, 0, content.Length, "file", "items.xls");

        var result = await controller.PreflightImport(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal(7, import.PreflightCompanyId);
        // The import service only ever parses .xlsx, so the controller must hand it converted bytes.
        using var received = new XLWorkbook(new MemoryStream(import.PreflightWorkbook!));
        Assert.Equal("GMK Item ID", received.Worksheet("Item Mappings").Cell(1, 1).GetString());
    }

    [Fact]
    public async Task Import_converts_a_legacy_xls_upload_before_writing()
    {
        var import = new CapturingImportService();
        var controller = CreateController(new CapturingItemMappingService(), import);
        await using var content = new MemoryStream(LegacyWorkbook());
        var file = new FormFile(content, 0, content.Length, "file", "items.xls");

        var result = await controller.Import(7, file, CancellationToken.None);

        Assert.IsType<JsonResult>(result);
        Assert.Equal((7, true, "admin"), import.ImportRequest);
        using var received = new XLWorkbook(new MemoryStream(import.ImportWorkbook!));
        Assert.Equal("GMK Item ID", received.Worksheet("Item Mappings").Cell(1, 1).GetString());
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

    private static byte[] ValidWorkbook()
    {
        using var workbook = new XLWorkbook();
        workbook.Worksheets.Add("Item Mappings").Cell(1, 1).Value = "GMK Item ID";
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static byte[] LegacyWorkbook()
    {
        using var workbook = new HSSFWorkbook();
        workbook.CreateSheet("Item Mappings").CreateRow(0).CreateCell(0).SetCellValue("GMK Item ID");
        using var stream = new MemoryStream();
        workbook.Write(stream, leaveOpen: true);
        return stream.ToArray();
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

        public UnmappedDistributorItemListDto Unmapped { get; init; } = new();
        public int? ListedUnmappedCompanyId { get; private set; }

        public Task<ServiceResponse<UnmappedDistributorItemListDto>> ListUnmappedAsync(int companyId,
            CancellationToken token)
        {
            ListedUnmappedCompanyId = companyId;
            return Task.FromResult(new ServiceResponse<UnmappedDistributorItemListDto> { Data = Unmapped });
        }

        public Task<ServiceResponse<ItemMappingDto?>> GetAsync(int id, int companyId, CancellationToken token)
        {
            var mapping = Mappings.SingleOrDefault(value => value.Id == id && value.CompanyId == companyId);
            return Task.FromResult(new ServiceResponse<ItemMappingDto?>
            {
                Data = mapping,
                StatusCode = mapping is null ? StatusCodes.Status404NotFound : StatusCodes.Status200OK
            });
        }

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
