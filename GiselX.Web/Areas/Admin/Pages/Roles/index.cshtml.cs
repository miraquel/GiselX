using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Roles;

[Authorize(PermissionConstants.Roles.Index)]
public class IndexModel : PageModel
{
    private readonly RoleManager<AppIdentityRole> _roleManager;

    public IndexModel(RoleManager<AppIdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    public IList<AppIdentityRole> Roles { get; set; } = new List<AppIdentityRole>();

    [BindProperty(SupportsGet = true)]
    public int PageNumber { get; set; } = 1;

    [BindProperty(SupportsGet = true)]
    public int PageSize { get; set; } = 10;

    public int TotalCount { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    public Task<IActionResult> OnGetAsync()
    {
        var rolesRaw = _roleManager.Roles;
        TotalCount = rolesRaw.Count();
        Roles = rolesRaw
            .OrderBy(r => r.Name)
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToList();
        return Task.FromResult<IActionResult>(Page());
    }
}