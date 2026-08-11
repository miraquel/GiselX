using GiselX.Service.Dto.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace GiselX.Tests;

public sealed class PagedListRequestDtoValidationTests
{
    [Fact]
    public void Null_search_term_is_valid_for_api_model_binding()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IObjectModelValidator>();
        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(
            new DefaultHttpContext { RequestServices = provider },
            new RouteData(),
            new ActionDescriptor(),
            modelState);

        validator.Validate(actionContext, null, string.Empty,
            new PagedListRequestDto { SearchTerm = null });

        Assert.DoesNotContain(modelState, entry =>
            entry.Key == nameof(PagedListRequestDto.SearchTerm) && entry.Value!.Errors.Count > 0);
    }
}
