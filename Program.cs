// using 引入其他文件/框架中的类，不会在这一行就连接数据库。
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Threading.RateLimiting;

// 创建网站配置器：默认读取 appsettings、开发配置、User Secrets 和环境变量。
var builder = WebApplication.CreateBuilder(args);

// 注册 MVC：Controller 处理请求，View 生成网页。
builder.Services.AddControllersWithViews();
// 保留 Identity 自带 Razor Pages 的支持。
builder.Services.AddRazorPages();

// 2. 注册数据库上下文
var connStr = builder.Configuration.GetConnectionString("CampusMarketDb");
// AddDbContext 让 Controller 可以通过构造函数获得 _db。
builder.Services.AddDbContext<ApplicationDbContext>(opts =>
    // 指定数据库驱动和连接地址；这里配置，真正查询时才发 SQL。
    opts.UseSqlServer(connStr));

// 3. 开启 Identity + 角色管理
builder.Services.AddDefaultIdentity<AppUser>(opts =>
{
    // 至少包含数字。
    opts.Password.RequireDigit = true;
    // 至少包含小写字母。
    opts.Password.RequireLowercase = true;
    // 至少包含大写字母。
    opts.Password.RequireUppercase = true;
    // 不强制要求符号。
    opts.Password.RequireNonAlphanumeric = false;
    // 最短 8 个字符。
    opts.Password.RequiredLength = 8;

    // 用户名使用邮箱，这里规定允许出现的字符。
    opts.User.AllowedUserNameCharacters = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ@._";

})
// 启用 Student/Admin 角色。
.AddRoles<IdentityRole>()
// 让 Identity 通过同一个 DbContext 保存用户、角色和密码哈希。
.AddEntityFrameworkStores<ApplicationDbContext>();

// 4. 授权策略
builder.Services.AddAuthorization(opts =>
{
    // AdminController 使用这个策略，普通登录用户仍然无法访问。
    opts.AddPolicy("AdminOnly", p => p.RequireRole(UserRoles.Admin));
    // 保留学生策略，其他控制器可以按需使用。
    opts.AddPolicy("StudentOnly", p => p.RequireRole(UserRoles.Student));
});

builder.Services.ConfigureApplicationCookie(options =>
{
    // 没登录时跳转到登录页。
    options.LoginPath = "/Account/Login";
    // 已登录但权限不够时跳转到提示页。
    options.AccessDeniedPath = "/Account/AccessDenied";
});

// HttpClient 负责发送 AI 请求；不自动跟随重定向，避免密钥被带到另一个地址。
builder.Services.AddHttpClient<ListingAssistantService>(client =>
    {
        // 最多等待 20 秒，避免 AI 服务没有响应时网页一直转圈。
        client.Timeout = TimeSpan.FromSeconds(20);
        // 只接受小文本响应，避免意外下载巨大的内容。
        client.MaxResponseContentBufferSize = 64 * 1024;
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });

// AI 接口按登录用户限流，每分钟最多 5 次，防止重复点击产生过多请求。
builder.Services.AddRateLimiter(options =>
{
    // 超额请求返回 HTTP 429。
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    // 策略名称与 ListingAssistantController 上的特性一致。
    options.AddPolicy("listing-assistant", context =>
    {
        // 用用户 ID 分组，各用户分别计数；未登录用户共用 anonymous。
        string key = context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous";
        // 固定窗口：每分钟重新获得 5 次机会，不排队等待。
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0
        });
    });
});

// 根据前面的注册和配置创建网站对象。
var app = builder.Build();

// 初始化角色（程序启动自动创建 Student、Admin 角色）
using (var scope = app.Services.CreateScope())
{
    // scope 限定这些数据库服务的生命周期，退出花括号会释放。
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // 只执行尚未应用的 Migration；不会清空已有账号或商品。
    await db.Database.MigrateAsync();
    // Identity 提供角色管理器，避免自己写角色相关 SQL。
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    // 如果 Student/Admin 不存在就创建。
    await InitRolesAsync(roleManager);

    // 本地命令行可以把一个已注册账号设为管理员；这不是公开网页接口。
    int adminArgument = Array.IndexOf(args, "--make-admin");
    if (adminArgument >= 0)
    {
        // 命令必须带邮箱，防止意外提升错误账号。
        if (adminArgument + 1 >= args.Length)
            throw new InvalidOperationException("Usage: dotnet run -- --make-admin student@massey.ac.nz");
        // 根据明确指定的邮箱查找已有账号，不自动创建默认密码。
        var users = scope.ServiceProvider.GetRequiredService<UserManager<AppUser>>();
        var user = await users.FindByEmailAsync(args[adminArgument + 1]);
        if (user is null) throw new InvalidOperationException("Register this account before making it an admin.");
        // 已经有角色就不重复写入。
        if (!await users.IsInRoleAsync(user, UserRoles.Admin))
        {
            var result = await users.AddToRoleAsync(user, UserRoles.Admin);
            if (!result.Succeeded) throw new InvalidOperationException("Could not assign the Admin role.");
        }
        // 提示重新登录，让新 Cookie 包含 Admin 角色。
        Console.WriteLine("Admin role assigned. Log out and log in again to use /Admin.");
        // 管理命令到这里结束，不启动网页监听。
        return;
    }
}

// 请求流程
if (!app.Environment.IsDevelopment())
{
    // 正式环境显示友好的错误页，不公开堆栈。
    app.UseExceptionHandler("/Home/Error");
    // 告诉浏览器后续优先使用 HTTPS。
    app.UseHsts();
}

// HTTP 请求转 HTTPS；本地 http 启动配置仍可用于调试。
app.UseHttpsRedirection();
// 允许浏览器访问 wwwroot 里的 CSS、JS 和商品照片。
app.UseStaticFiles();
// 根据网址匹配控制器方法。
app.UseRouting();
// 先确定登录者是谁。
app.UseAuthentication();
// 再检查是否有权访问目标方法。
app.UseAuthorization();
// 身份确定后，再按用户应用 AI 请求限流。
app.UseRateLimiter();

// /Listings/Details/5 对应 ListingsController.Details(5)，首页默认 Home/Index。
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
// 注册 Razor Pages 的网址。
app.MapRazorPages();

// 开始监听网页请求，直到程序被停止。
app.Run();
// async 表示可等待数据库操作；Task 表示异步方法没有直接返回数据。
async Task InitRolesAsync(RoleManager<IdentityRole> roleMgr)
{
    // 这两个字符串和 UserRoles.cs（实际定义在 AppUser.cs）中的常量一致。
    string[] roles = { UserRoles.Student, UserRoles.Admin };
    // 依次处理角色数组里的每个角色名。
    foreach (var r in roles)
    {
        // 已存在时不重复创建。
        if (!await roleMgr.RoleExistsAsync(r))
        {
            // 角色管理器会通过 EF Core 写入 AspNetRoles。
            var result = await roleMgr.CreateAsync(new IdentityRole(r));
            // 创建失败时给出明确错误，避免运行到注册时才发现角色不存在。
            if (!result.Succeeded) throw new InvalidOperationException("Could not create role: " + r);
        }
    }
}
