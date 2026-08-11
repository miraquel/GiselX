using System.Globalization;
using System.Security.Claims;
using GiselX.Domain;
using GiselX.Service.Dto;
using GiselX.Service.Dto.Common;
using GiselX.Service.Interface;
using GiselX.Web.Controllers;
using GiselX.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;

namespace GiselX.Tests;

public sealed class DistributorTemplateMappingControllerTests
{
    [Fact]
    public async Task Index_ReturnsMappingsForRequestedCompany()
    {
        var service = new CapturingMappingService
        {
            ListedMappings = [new DistributorTemplateMappingDto { Id = 7, CompanyId = 2, Name = "Weekly" }]
        };
        var controller = CreateController(service);

        var result = await controller.Index(2, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributorTemplateMappingListViewModel>(view.Model);
        Assert.Equal(2, model.CompanyId);
        Assert.Single(model.Mappings);
        Assert.Equal(2, service.ListedCompanyId);
    }

    [Fact]
    public async Task Delete_RedirectsToCompanyMappingListWithServiceMessage()
    {
        var service = new CapturingMappingService();
        var controller = CreateController(service);

        var result = await controller.Delete(7, 2, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
        Assert.Equal(2, redirect.RouteValues!["companyId"]);
        Assert.Equal((7, 2), service.DeletedMapping);
        Assert.Equal("Mapping deleted.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Activate_ForwardsTrueCompanyMappingAndAuthenticatedUsername()
    {
        var service = new CapturingMappingService();
        var controller = CreateController(service);

        var result = await controller.Activate(7, 2, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
        Assert.Equal(2, redirect.RouteValues!["companyId"]);
        Assert.Equal((7, 2, true, "admin"), service.ActiveChange);
        Assert.Equal("Mapping activated.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Deactivate_ForwardsFalseCompanyMappingAndAuthenticatedUsername()
    {
        var service = new CapturingMappingService();
        var controller = CreateController(service);

        var result = await controller.Deactivate(7, 2, CancellationToken.None);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal(nameof(DistributorTemplateMappingsController.Index), redirect.ActionName);
        Assert.Equal(2, redirect.RouteValues!["companyId"]);
        Assert.Equal((7, 2, false, "admin"), service.ActiveChange);
        Assert.Equal("Mapping deactivated.", controller.TempData["StatusMessage"]);
    }

    [Fact]
    public async Task Edit_IncludesStoredSourceHeadersAndAssignments()
    {
        var service = new CapturingMappingService
        {
            Mapping = new DistributorTemplateMappingDto
            {
                Id = 7,
                CompanyId = 2,
                Columns =
                [
                    new DistributorTemplateColumnDto
                    {
                        SourceColumnNumber = 4,
                        SourceHeader = "Invoice Number",
                        CanonicalField = SelloutCanonicalField.InvoiceNo
                    }
                ]
            }
        };
        var controller = CreateController(service);

        var result = await controller.Edit(7, 2, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributorTemplateMappingEditViewModel>(view.Model);
        var column = Assert.Single(model.Columns);
        Assert.Equal("Invoice Number", column.SourceHeader);
        Assert.Equal(SelloutCanonicalField.InvoiceNo, column.CanonicalField);
    }

    [Fact]
    public async Task Binding_AllowsIgnoredColumnBeforeMappedColumn()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var binderFactory = services.GetRequiredService<IModelBinderFactory>();
        var metadata = metadataProvider.GetMetadataForType(typeof(DistributorTemplateMappingEditViewModel));
        var binder = binderFactory.CreateBinder(new ModelBinderFactoryContext { Metadata = metadata });
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Columns[0].SourceColumnNumber"] = "1",
            ["Columns[0].CanonicalField"] = "",
            ["Columns[1].SourceColumnNumber"] = "2",
            ["Columns[1].CanonicalField"] = "0"
        });
        var httpContext = new DefaultHttpContext { RequestServices = services };
        var actionContext = new ActionContext(httpContext, new RouteData(), new ActionDescriptor(),
            new ModelStateDictionary());
        var bindingContext = DefaultModelBindingContext.CreateBindingContext(actionContext,
            new FormValueProvider(BindingSource.Form, form, CultureInfo.InvariantCulture), metadata,
            bindingInfo: null, modelName: string.Empty);

        await binder.BindModelAsync(bindingContext);

        var model = Assert.IsType<DistributorTemplateMappingEditViewModel>(bindingContext.Result.Model);
        Assert.Equal(0, actionContext.ModelState.ErrorCount);
        Assert.Equal(2, model.Columns.Count);
        Assert.Equal(SelloutCanonicalField.CustomerId, model.Columns[1].CanonicalField);
    }

    [Fact]
    public async Task Save_IgnoresPlaceholderColumnAndForwardsMappedColumns()
    {
        var mappingService = new CapturingMappingService();
        var controller = CreateController(mappingService);
        var model = new DistributorTemplateMappingEditViewModel
        {
            CompanyId = 2,
            Name = "Mapping Sellout",
            WorksheetName = "Data",
            HeaderRowNumber = 4,
            FirstDataRowNumber = 5,
            Columns =
            [
                new DistributorTemplateColumnEditViewModel
                {
                    SourceColumnNumber = 1
                },
                Column(2, SelloutCanonicalField.CustomerId),
                Column(3, SelloutCanonicalField.CustomerName),
                Column(4, SelloutCanonicalField.ProductId),
                Column(5, SelloutCanonicalField.ProductName),
                Column(6, SelloutCanonicalField.InvoiceNo),
                Column(7, SelloutCanonicalField.InvoiceDate),
                Column(8, SelloutCanonicalField.InvoiceQty),
                Column(9, SelloutCanonicalField.InvoiceUnit)
            ]
        };
        await using var workbook = new MemoryStream([1]);
        var sampleFile = new FormFile(workbook, 0, workbook.Length, "sampleFile", "sample.xlsx");

        var result = await controller.Save(model, sampleFile, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = Assert.IsType<SaveDistributorTemplateMappingDto>(mappingService.SavedMapping);
        Assert.Equal(8, saved.Columns.Count);
        Assert.DoesNotContain(saved.Columns, column => !Enum.IsDefined(column.CanonicalField));
        Assert.Equal(2, saved.Columns[0].SourceColumnNumber);
    }

    private static DistributorTemplateColumnEditViewModel Column(int sourceColumnNumber, SelloutCanonicalField field) =>
        new() { SourceColumnNumber = sourceColumnNumber, CanonicalField = field };

    private static DistributorTemplateMappingsController CreateController(CapturingMappingService mappingService)
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.Name, "admin")], "TestAuthentication"))
        };
        return new DistributorTemplateMappingsController(mappingService, new SuccessfulInspector())
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, new InMemoryTempDataProvider())
        };
    }

    private sealed class SuccessfulInspector : ISelloutWorkbookInspector
    {
        public Task<SelloutWorkbookPreviewDto> InspectAsync(byte[] workbook, string? worksheetName,
            int headerRowNumber, int firstDataRowNumber, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SelloutWorkbookPreviewDto
            {
                WorksheetName = "Data",
                HeaderSignature = "HEADER-SIGNATURE",
                Headers = Enumerable.Range(1, 9).Select(index => $"Header {index}").ToList()
            });

        public Task<SelloutMappingDetectionResultDto> DetectAsync(byte[] workbook,
            IReadOnlyCollection<DistributorTemplateMappingDto> mappings,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class CapturingMappingService : IDistributorTemplateMappingService
    {
        public SaveDistributorTemplateMappingDto? SavedMapping { get; private set; }
        public IReadOnlyList<DistributorTemplateMappingDto> ListedMappings { get; init; } = [];
        public DistributorTemplateMappingDto? Mapping { get; init; }
        public int? ListedCompanyId { get; private set; }
        public (int Id, int CompanyId)? DeletedMapping { get; private set; }
        public (int Id, int CompanyId, bool IsActive, string Username)? ActiveChange { get; private set; }

        public Task<ServiceResponse<int>> SaveAsync(SaveDistributorTemplateMappingDto mapping, string username,
            CancellationToken cancellationToken)
        {
            SavedMapping = mapping;
            return Task.FromResult(new ServiceResponse<int> { Data = 42 });
        }

        public Task<ServiceResponse<IReadOnlyList<DistributorTemplateMappingDto>>> ListAsync(int companyId,
            CancellationToken cancellationToken)
        {
            ListedCompanyId = companyId;
            return Task.FromResult(new ServiceResponse<IReadOnlyList<DistributorTemplateMappingDto>>
            {
                Data = ListedMappings
            });
        }

        public Task<ServiceResponse<DistributorTemplateMappingDto?>> GetAsync(int id, int companyId,
            CancellationToken cancellationToken) => Task.FromResult(new ServiceResponse<DistributorTemplateMappingDto?>
            {
                Data = Mapping
            });

        public Task<ServiceResponse> SetActiveAsync(int id, int companyId, bool isActive, string username,
            CancellationToken cancellationToken)
        {
            ActiveChange = (id, companyId, isActive, username);
            return Task.FromResult(new ServiceResponse { Message = isActive ? "Mapping activated." : "Mapping deactivated." });
        }

        public Task<ServiceResponse> DeleteAsync(int id, int companyId,
            CancellationToken cancellationToken)
        {
            DeletedMapping = (id, companyId);
            return Task.FromResult(new ServiceResponse { Message = "Mapping deleted." });
        }
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();
        public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
    }
}
