using GiselX.Common.Constants;
using Hangfire.Dashboard;

namespace GiselX.Web;

public class HangfireAdminAuthFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();
        return httpContext.User.Identity?.IsAuthenticated == true
               && httpContext.User.HasClaim("permission", PermissionConstants.Hangfire.Dashboard);
    }
}
