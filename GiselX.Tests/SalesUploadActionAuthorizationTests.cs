using System.Reflection;
using GiselX.Common.Constants;
using GiselX.Web.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Tests;

/// <summary>
/// DownloadSource was inserted between UploadResult and the attributes that decorated it, which left
/// UploadResult open to any signed-in user and made DownloadSource demand Upload as well as Index. The
/// compiler cannot see that — attributes attach to whichever member follows them — so pin it here.
/// </summary>
public sealed class SalesUploadActionAuthorizationTests
{
    [Fact]
    public void UploadResult_requires_the_upload_permission()
    {
        Assert.Equal([PermissionConstants.SalesTransactions.Upload], Policies(nameof(SalesTransactionsController.UploadResult)));
    }

    [Fact]
    public void DownloadSource_requires_only_the_index_permission_like_its_stock_counterpart()
    {
        Assert.Equal([PermissionConstants.SalesTransactions.Index], Policies(nameof(SalesTransactionsController.DownloadSource)));
    }

    private static string[] Policies(string action) => typeof(SalesTransactionsController)
        .GetMethod(action, BindingFlags.Public | BindingFlags.Instance)!
        .GetCustomAttributes<AuthorizeAttribute>()
        .Select(attribute => attribute.Policy ?? string.Empty)
        .ToArray();
}
