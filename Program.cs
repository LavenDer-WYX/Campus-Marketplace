using WebApplication1.Data;
using WebApplication1.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. MVC 视图支持
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// 2. 注册数据库上下文
var connStr = builder.Configuration.GetConnectionString("CampusMarketDb");
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    opts.UseSqlServer(connStr));

// 3. 开启 Identity + 角色管理
builder.Services.AddDefaultIdentity<AppUser>(opts =>
{
    // 密码规则
    opts.Password.RequireDigit = true;
    opts.Password.RequireLowercase = true;
    opts.Password.RequireUppercase = true;
    opts.Password.RequireNonAlphanumeric = false;
    opts.Password.RequiredLength = 8;

    opts.User.AllowedUserNameCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ@._";

})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

// 4. 授权策略
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

var app = builder.Build();

// 初始化角色（程序启动自动创建 Student、Admin 角色）
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await db.Database.MigrateAsync();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    await InitRolesAsync(roleManager);
}

// 请求流程
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
            await roleMgr.CreateAsync(new IdentityRole(r));
        }
    }
}
