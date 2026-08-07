using System.Security.Claims;
using GiselX.Common.Constants;
using GiselX.Domain;
using GiselX.Repository;
using GiselX.Service;
using GiselX.Service.Dto.Common;
using GiselX.Web;
using GiselX.Web.Context;
using Hangfire;
using Hangfire.Dashboard;
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

// Email settings
builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("Email"));

// Hangfire
builder.Services.AddHangfire(config =>
    config.UseSqlServerStorage(connectionString));
builder.Services.AddHangfireServer();

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

// First-run bootstrap: seed an admin user (with a role holding every permission)
// when the user table is empty. Credentials come from the "AdminSeed" config section
// so they can be overridden via appsettings/environment/user-secrets.
await SeedFirstAdminAsync(app);

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

// Hangfire dashboard (requires Hangfire.Dashboard permission)
app.MapHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new HangfireAdminAuthFilter()]
});

// Register daily reminder recurring job
var dailyJobHour = app.Configuration.GetValue<int>("Hangfire:DailyJobHour", 8);
Hangfire.RecurringJob.AddOrUpdate<ReminderJob>(
    "daily-reminder",
    job => job.ExecuteAsync(),
    $"0 {dailyJobHour} * * *");

app.Run();

return;

// Seeds a default company, an Administrator role holding every permission, and a
// confirmed admin user assigned to that role — but only when no users exist yet.
static async Task SeedFirstAdminAsync(WebApplication webApp)
{
    var seedConfig = webApp.Configuration.GetSection("AdminSeed");
    if (!seedConfig.GetValue("Enabled", true)) return;

    using var scope = webApp.Services.CreateScope();
    var services = scope.ServiceProvider;

    var db = services.GetRequiredService<GiselXDbContext>();
    await db.Database.MigrateAsync();

    var userManager = services.GetRequiredService<UserManager<AppIdentityUser>>();
    var roleManager = services.GetRequiredService<RoleManager<AppIdentityRole>>();

    // Only bootstrap when the system is started for the first time (empty user list).
    if (await userManager.Users.AnyAsync()) return;

    var logger = services.GetRequiredService<ILogger<Program>>();

    var userName = seedConfig.GetValue("UserName", "admin")!;
    var email = seedConfig.GetValue("Email", "admin@giselx.local")!;
    var password = seedConfig.GetValue("Password", string.Empty)!;
    var roleName = seedConfig.GetValue("RoleName", "Administrator")!;
    var companyName = seedConfig.GetValue("CompanyName", "Default Company")!;

    // Fail safe: never seed with a blank/source-controlled password. The password must be
    // supplied out-of-band — user-secrets in development, AdminSeed__Password in production.
    if (string.IsNullOrWhiteSpace(password))
    {
        logger.LogWarning(
            "Admin seed skipped: no admin password configured. Set 'AdminSeed:Password' via " +
            "user-secrets (development) or the AdminSeed__Password environment variable (production).");
        return;
    }

    // 1. CompanyId is a required FK on AppIdentityUser — ensure a company exists.
    var company = await db.Set<Company>().FirstOrDefaultAsync();
    if (company is null)
    {
        company = new Company { Name = companyName };
        db.Add(company);
        await db.SaveChangesAsync();
    }

    // 2. Administrator role holding every permission claim.
    var role = await roleManager.FindByNameAsync(roleName);
    if (role is null)
    {
        role = new AppIdentityRole { Name = roleName, Description = "Full access (seeded)" };
        var roleResult = await roleManager.CreateAsync(role);
        if (!roleResult.Succeeded)
        {
            logger.LogError("Failed to seed admin role: {Errors}",
                string.Join("; ", roleResult.Errors.Select(e => e.Description)));
            return;
        }
    }

    var existingClaims = (await roleManager.GetClaimsAsync(role))
        .Where(c => c.Type == "permission")
        .Select(c => c.Value)
        .ToHashSet();
    foreach (var permission in PermissionConstants.GetAllPermissions())
    {
        if (existingClaims.Add(permission))
            await roleManager.AddClaimAsync(role, new Claim("permission", permission));
    }

    // 3. First admin user — EmailConfirmed so it can log in immediately.
    var admin = new AppIdentityUser
    {
        UserName = userName,
        Email = email,
        EmailConfirmed = true,
        FirstName = "System",
        LastName = "Administrator",
        CompanyId = company.Id
    };

    var userResult = await userManager.CreateAsync(admin, password);
    if (!userResult.Succeeded)
    {
        logger.LogError("Failed to seed admin user: {Errors}",
            string.Join("; ", userResult.Errors.Select(e => e.Description)));
        return;
    }

    await userManager.AddToRoleAsync(admin, roleName);
    logger.LogInformation("Seeded first admin user '{UserName}' with role '{Role}'.", userName, roleName);
}
