using GiselX.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using GiselX.Common.Constants;
using Microsoft.AspNetCore.Authorization;

namespace GiselX.Web.Areas.Admin.Pages.Users;

[Authorize(PermissionConstants.Users.Create)]
public class CreateModel : PageModel
{
    private readonly UserManager<AppIdentityUser> _userManager;

    public CreateModel(UserManager<AppIdentityUser> userManager)
    {
        _userManager = userManager;
    }

    [BindProperty]
    [Required]
    [Display(Name = "Username")]
    public string UserName { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [BindProperty]
    [Display(Name = "Last Name")]
    public string? LastName { get; set; }

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [BindProperty]
    [Required]
    [DataType(DataType.Password)]
    [Display(Name = "Confirm Password")]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    public string ConfirmPassword { get; set; } = string.Empty;

    [BindProperty]
    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }
    
    [BindProperty]
    [Required]
    [Display(Name = "Company")]
    public Company Company { get; set; } = new();

    [TempData]
    public string? StatusMessage { get; set; }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
        {
            return Page();
        }
        
        var user = new AppIdentityUser
        {
            UserName = UserName,
            Email = Email,
            FirstName = FirstName,
            LastName = LastName,
            PhoneNumber = PhoneNumber,
            EmailConfirmed = true,
            PhoneNumberConfirmed = !string.IsNullOrWhiteSpace(PhoneNumber),
            Company = Company
        };
        var result = await _userManager.CreateAsync(user, Password);
        if (result.Succeeded)
        {
            StatusMessage = "User created successfully.";
            return RedirectToPage("Index");
        }
        foreach (var error in result.Errors)
        {
            ModelState.AddModelError(string.Empty, error.Description);
        }
        return Page();
    }
}