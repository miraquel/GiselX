using System.Security.Claims;
using GiselX.Common.Constants;
using GiselX.Domain;
using GiselX.Repository;
using GiselX.Service;
using GiselX.Service.Dto.Common;
using GiselX.Web.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ??
                       throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<GiselXDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<AppIdentityUser>(options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<AppIdentityRole>()
    .AddEntityFrameworkStores<GiselXDbContext>();

builder.Services.AddAuthentication()
    .AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddAuthorization(options =>
{
    var permissions = PermissionConstants.GetAllPermissions();

    if (permissions.Count == 0) return;
    
    foreach (var permission in permissions)
    {
        options.AddPolicy(permission, policy => policy.RequireClaim("permission", permission));
    }
});

builder.Services.AddControllersWithViews();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Example: Auto-logout after 30 minutes of inactivity
    options.SlidingExpiration = true; // Extends the cookie lifetime on activity
    options.LoginPath = "/Identity/Account/Login"; // Redirect path after logout
});

builder.Services.AddGiselXRepository();
builder.Services.AddGiselXService();

builder.Services.AddScoped(serviceProvider =>
{
    var httpContextAccessor = serviceProvider.GetRequiredService<IHttpContextAccessor>();
    var username = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name)?.Value ?? "";
    _ = Guid.TryParse(httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value, out var userId);
    var userClaimDto = new UserClaimDto
    {
        UserId = userId,
        Username = username
    };

    return userClaimDto;
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

// app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapAreaControllerRoute(
    name: "Admin",
    areaName: "Admin",
    pattern: "Admin/{controller=Home}/{action=Index}/{id?}");

app.MapControllerRoute(
        name: "default",
        pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapRazorPages()
    .WithStaticAssets();

app.Run();