using System.Security.Claims;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace GiselX.Tests;

public sealed class StockTemplateMappingControllerTests
{
    [Fact]
    public async Task Index_preserves_requested_company()
    {
        var service = new CapturingService();
        var controller = CreateController(service);

        var result = await controller.Index(8, CancellationToken.None);

        var model = Assert.IsType<StockTemplateMappingListViewModel>(Assert.IsType<ViewResult>(result).Model);
        Assert.Equal(8, model.CompanyId);
        Assert.Equal(8, service.ListedCompanyId);
    }

    [Fact]
    public async Task Activate_forwards_true_company_mapping_and_authenticated_username()
    {
        var service = new CapturingService();
        var controller = CreateController(service);

        var result = await controller.Activate(7, 8, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(StockTemplateMappingsController.Index), redirect.ActionName);
        Assert.Equal(8, redirect.RouteValues!["companyId"]);
        Assert.Equal((7, 8, true, "admin"), service.ActiveChange!.Value);
        Assert.Equal("Mapping activated.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Deactivate_forwards_false_company_mapping_and_authenticated_username()
    {
        var service = new CapturingService();
        var controller = CreateController(service);

        await controller.Deactivate(7, 8, CancellationToken.None);

        Assert.Equal((7, 8, false, "admin"), service.ActiveChange!.Value);
        Assert.Equal("Mapping deactivated.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Save_forwards_only_explicitly_assigned_columns()
    {
        var service = new CapturingService();
        var controller = CreateController(service);
        var model = new StockTemplateMappingEditViewModel
        {
            CompanyId = 8,
            Name = "Stock",
            WorksheetName = "Data",
            HeaderRowNumber = 1,
            FirstDataRowNumber = 2,
            Columns =
            [
                new() { SourceColumnNumber = 1 },
                new() { SourceColumnNumber = 2, CanonicalField = StockCanonicalField.ProductId }
            ]
        };
        await using var workbook = new MemoryStream([1]);
        var file = new FormFile(workbook, 0, workbook.Length, "sampleFile", "sample.xlsx");

        await controller.Save(model, file, CancellationToken.None);

        var saved = Assert.IsType<SaveStockTemplateMappingDto>(service.Saved);
        var column = Assert.Single(saved.Columns);
        Assert.Equal(2, column.SourceColumnNumber);
        Assert.Equal(StockCanonicalField.ProductId, column.CanonicalField);
    }

    private static StockTemplateMappingsController CreateController(CapturingService service)
    {
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")], "Test"))
        };
        return new StockTemplateMappingsController(service, new Inspector())
        {
            ControllerContext = new ControllerContext { HttpContext = context },
            TempData = new TempDataDictionary(context, new TempDataProvider())
        };
    }

    private sealed class Inspector : IStockWorkbookInspector
    {
        public Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
            int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SelloutWorkbookPreviewDto
            {
                WorksheetName = "Data", HeaderSignature = "SIGNATURE", Headers = ["Ignore", "Product"]
            });
        public Task<StockMappingDetectionResultDto> DetectAsync(byte[] workbook,
            IReadOnlyCollection<StockTemplateMappingDto> mappings, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class CapturingService : IStockTemplateMappingService
    {
        public int? ListedCompanyId { get; private set; }
        public SaveStockTemplateMappingDto? Saved { get; private set; }
        public (int Id, int CompanyId, bool IsActive, string Username)? ActiveChange { get; private set; }
        public Task<ServiceResponse<IReadOnlyList<StockTemplateMappingDto>>> ListAsync(int companyId, CancellationToken cancellationToken)
        {
            ListedCompanyId = companyId;
            return Task.FromResult(new ServiceResponse<IReadOnlyList<StockTemplateMappingDto>> { Data = [] });
        }
        public Task<ServiceResponse<StockTemplateMappingDto?>> GetAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceResponse<StockTemplateMappingDto?>());
        public Task<ServiceResponse<int>> SaveAsync(SaveStockTemplateMappingDto mapping, string username, CancellationToken cancellationToken)
        {
            Saved = mapping;
            return Task.FromResult(new ServiceResponse<int> { Data = 1 });
        }
        public Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
            CancellationToken cancellationToken)
        {
            ActiveChange = (id, companyId, isActive, username);
            return Task.FromResult(new ServiceResponse
            {
                Message = isActive ? "Mapping activated." : "Mapping deactivated."
            });
        }
        public Task<ServiceResponse> DeleteAsync(int id, int companyId, CancellationToken cancellationToken) =>
            Task.FromResult(new ServiceResponse());
    }

    private sealed class TempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
