using System.ComponentModel.DataAnnotations;
using GiselX.Common.Constants;
using GiselX.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiselX.Web.Areas.Admin.Pages.Roles;

[Authorize(PermissionConstants.Roles.Create)]
public class CreateModel : PageModel
{
    private readonly RoleManager<AppIdentityRole> _roleManager;

    public CreateModel(RoleManager<AppIdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }
    
    [BindProperty]
    public InputModel Input { get; set; } = new();
    
    public class InputModel
    {
        [Required]
        [Display(Name = "Role Name")]
        public string RoleName { get; set; } = string.Empty;

        [Display(Name = "Role Description")]
        public string RoleDescription { get; set; } = string.Empty;
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();
        
        var roleExists = await _roleManager.RoleExistsAsync(Input.RoleName);
        if (roleExists)
        {
            ModelState.AddModelError(string.Empty, "Role already exists.");
            return Page();
        }
        
        var role = new AppIdentityRole
        {
            Name = Input.RoleName,
            Description = Input.RoleDescription
        };

        var result = await _roleManager.CreateAsync(role);
        if (result.Succeeded)
            return RedirectToPage("Index");

        foreach (var error in result.Errors)
            ModelState.AddModelError(string.Empty, error.Description);

        return Page();
    }
}

