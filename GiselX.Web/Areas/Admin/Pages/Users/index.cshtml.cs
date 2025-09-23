using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Index)]
public class IndexModel : PageModel;