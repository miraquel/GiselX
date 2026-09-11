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
    public void SourceHeader_IsOptionalForUnmappedCanonicalRows()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var metadata = metadataProvider.GetMetadataForProperty(
            typeof(DistributorTemplateColumnEditViewModel),
            nameof(DistributorTemplateColumnEditViewModel.SourceHeader));

        Assert.False(metadata.IsRequired);
    }

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
    public void Create_IncludesEveryCanonicalFieldAsAnUnmappedRow()
    {
        var controller = CreateController(new CapturingMappingService());

        var result = controller.Create(2);

        var view = Assert.IsType<ViewResult>(result);
        var model = Assert.IsType<DistributorTemplateMappingEditViewModel>(view.Model);
        Assert.Equal(Enum.GetValues<SelloutCanonicalField>(), model.Columns.Select(column => column.CanonicalField!.Value));
        Assert.All(model.Columns, column => Assert.Equal(0, column.SourceColumnNumber));
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
        Assert.Equal(Enum.GetValues<SelloutCanonicalField>().Length, model.Columns.Count);
        var column = model.Columns.Single(column => column.CanonicalField == SelloutCanonicalField.InvoiceNo);
        Assert.Equal(4, column.SourceColumnNumber);
        Assert.Equal("Invoice Number", column.SourceHeader);
        Assert.Equal(SelloutCanonicalField.InvoiceNo, column.CanonicalField);
        Assert.Equal(0, model.Columns.Single(column =>
            column.CanonicalField == SelloutCanonicalField.CustomerId).SourceColumnNumber);
    }

    [Fact]
    public async Task Binding_AllowsUnmappedCanonicalFieldBeforeMappedCanonicalField()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        var metadataProvider = services.GetRequiredService<IModelMetadataProvider>();
        var binderFactory = services.GetRequiredService<IModelBinderFactory>();
        var metadata = metadataProvider.GetMetadataForType(typeof(DistributorTemplateMappingEditViewModel));
        var binder = binderFactory.CreateBinder(new ModelBinderFactoryContext { Metadata = metadata });
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Columns[0].SourceColumnNumber"] = "0",
            ["Columns[0].CanonicalField"] = "0",
            ["Columns[1].SourceColumnNumber"] = "2",
            ["Columns[1].CanonicalField"] = "1"
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
        Assert.Equal(SelloutCanonicalField.CustomerId, model.Columns[0].CanonicalField);
        Assert.Equal(0, model.Columns[0].SourceColumnNumber);
        Assert.Equal(SelloutCanonicalField.CustomerName, model.Columns[1].CanonicalField);
        Assert.Equal(2, model.Columns[1].SourceColumnNumber);
    }

    [Fact]
    public async Task Save_IgnoresUnmappedCanonicalRowsAndForwardsMappedRows()
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
                Column(SelloutCanonicalField.CustomerAlias, 0),
                Column(SelloutCanonicalField.CustomerId, 2),
                Column(SelloutCanonicalField.CustomerName, 2),
                Column(SelloutCanonicalField.ProductId, 4),
                Column(SelloutCanonicalField.ProductName, 5),
                Column(SelloutCanonicalField.InvoiceNo, 6),
                Column(SelloutCanonicalField.InvoiceDate, 7),
                Column(SelloutCanonicalField.InvoiceQty, 8),
                Column(SelloutCanonicalField.InvoiceUnit, 9)
            ]
        };
        await using var workbook = new MemoryStream([1]);
        var sampleFile = new FormFile(workbook, 0, workbook.Length, "sampleFile", "sample.xlsx");

        var result = await controller.Save(model, sampleFile, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        var saved = Assert.IsType<SaveDistributorTemplateMappingDto>(mappingService.SavedMapping);
        Assert.Equal(8, saved.Columns.Count);
        Assert.DoesNotContain(saved.Columns, column => !Enum.IsDefined(column.CanonicalField));
        Assert.Equal(2, saved.Columns.Count(column => column.SourceColumnNumber == 2));
    }

    private static DistributorTemplateColumnEditViewModel Column(SelloutCanonicalField field, int sourceColumnNumber) =>
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
