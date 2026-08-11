using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace GiselX.Tests;

public sealed class SalesTransactionUploadControllerTests
{
    [Fact]
    public void Upload_no_longer_accepts_a_reporting_date()
    {
        var action = typeof(SalesTransactionsController).GetMethod(nameof(SalesTransactionsController.Upload))!;

        Assert.DoesNotContain(action.GetParameters(), parameter => parameter.ParameterType == typeof(DateTime));
        Assert.Contains(action.GetParameters(), parameter => parameter.Name == "multiWeekConfirmed");
    }

    [Fact]
    public void PreflightUpload_is_an_antiforgery_protected_post()
    {
        var action = typeof(SalesTransactionsController).GetMethod("PreflightUpload");

        Assert.NotNull(action);
        Assert.NotNull(action!.GetCustomAttributes(typeof(HttpPostAttribute), true).SingleOrDefault());
        Assert.NotNull(action.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), true).SingleOrDefault());
    }

    [Fact]
    public void UploadResult_supports_group_and_legacy_batch_links()
    {
        var action = typeof(SalesTransactionsController).GetMethod(nameof(SalesTransactionsController.UploadResult))!;
        var parameters = action.GetParameters();

        Assert.Contains(parameters, parameter => parameter.Name == "groupId" && parameter.ParameterType == typeof(long?));
        Assert.Contains(parameters, parameter => parameter.Name == "batchId" && parameter.ParameterType == typeof(long?));
    }
}
