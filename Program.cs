
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;


var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();

builder.Services.AddRazorPages();


var connStr = builder.Configuration.GetConnectionString("CampusMarketDb");

builder.Services.AddDbContext<ApplicationDbContext>(opts =>

    opts.UseSqlServer(connStr));


builder.Services.AddDefaultIdentity<AppUser>(opts =>
{

    opts.Password.RequireDigit = true;

    opts.Password.RequireLowercase = true;

    opts.Password.RequireUppercase = true;

    opts.Password.RequireNonAlphanumeric = false;

    opts.Password.RequiredLength = 8;


    opts.User.AllowedUserNameCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ@._";

})

.AddRoles<IdentityRole>()

.AddEntityFrameworkStores<ApplicationDbContext>();


builder.Services.AddAuthorization(opts =>
{

    opts.AddPolicy("AdminOnly", p => p.RequireRole(UserRoles.Admin));

    opts.AddPolicy("StudentOnly", p => p.RequireRole(UserRoles.Student));
});

builder.Services.ConfigureApplicationCookie(options =>
{

    options.LoginPath = "/Account/Login";

    options.AccessDeniedPath = "/Account/AccessDenied";
});


builder.Services.AddHttpClient<ListingAssistantService>(client =>
    {

        client.Timeout = TimeSpan.FromSeconds(20);

        client.MaxResponseContentBufferSize = 64 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });


builder.Services.AddHttpClient<SmartSearchService>(client =>
    {
        client.Timeout = TimeSpan.FromSeconds(20);
        client.MaxResponseContentBufferSize = 64 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });


builder.Services.AddRateLimiter(options =>
{

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("listing-assistant", context =>
    {

        string key = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});


var app = builder.Build();


using (var scope = app.Services.CreateScope())
{

    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    await db.Database.MigrateAsync();

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    await InitRolesAsync(roleManager);


    int adminArgument = Array.IndexOf(args, "--make-admin");
    if (adminArgument >= 0)
    {

        if (adminArgument + 1 >= args.Length)
            throw new InvalidOperationException("Usage: dotnet run -- --make-admin student@massey.ac.nz");

        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(args[adminArgument + 1]);
        if (user is null) throw new InvalidOperationException("Register this account before making it an admin.");

        if (!await users.IsInRoleAsync(user, UserRoles.Admin))
        {
            var result = await users.AddToRoleAsync(user, UserRoles.Admin);
            if (!result.Succeeded) throw new InvalidOperationException("Could not assign the Admin role.");
        }

        Console.WriteLine("Admin role assigned. Log out and log in again to use /Admin.");

        return;
    }
}


if (!app.Environment.IsDevelopment())
{

    app.UseExceptionHandler("/Home/Error");

    app.UseHsts();
}


app.UseHttpsRedirection();

app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();

app.UseAuthorization();

app.UseRateLimiter();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();


app.Run();

async Task InitRolesAsync(RoleManager<IdentityRole> roleMgr)
{

    string[] roles = { UserRoles.Student, UserRoles.Admin };

    foreach (var r in roles)
    {

        if (!await roleMgr.RoleExistsAsync(r))
        {

            var result = await roleMgr.CreateAsync(new IdentityRole(r));

            if (!result.Succeeded) throw new InvalidOperationException("Could not create role: " + r);
        }
    }
}
