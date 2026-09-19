// using 引入其他命名空间里的类型，类似其他语言的 import。
using Microsoft.AspNetCore.Authorization; // 提供登录权限检查。
using Microsoft.AspNetCore.Identity; // 提供账号、密码、角色和登录管理。
using Microsoft.AspNetCore.Mvc; // 提供 Controller、View 和页面跳转。
using WebApplication1.Models; // 引入 AppUser 和注册、登录表单模型。

// namespace 给类分组，不会建立数据库，也不会运行登录功能。
namespace WebApplication1.Controllers;

// 负责 /Account/... 请求；继承 Controller 后可以使用 View() 等方法。
public class AccountController : Controller
{
    private const string CampusEmailDomain = "massey.ac.nz"; // const 表示固定不变的学校邮箱域名。
    private readonly UserManager<AppUser> _userManager; // 负责账号的保存、密码哈希和角色。
    private readonly SignInManager<AppUser> _signInManager; // 负责登录和退出时的身份 Cookie。

    // ASP.NET 把 Program.cs 注册的服务自动传进来，这叫“依赖注入”。
    public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager)
    {
        _userManager = userManager; // 保存账号服务；readonly 字段只能在这里或声明时赋值。
        _signInManager = signInManager; // 保存登录服务，供其他方法使用。
    }

    [HttpGet] // GET：用户打开页面，还没提交注册资料。
    [AllowAnonymous] // 未登录的人也可以打开注册页。
    public IActionResult Register(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl; // 把“注册后返回哪里”交给页面表单。
        return View(); // 返回 Views/Account/Register.cshtml。
    }

    [HttpPost] // POST：处理注册表单提交的数据。
    [AllowAnonymous] // 注册前没有账号，允许匿名访问。
    [ValidateAntiForgeryToken] // 检查本站表单令牌，防止其他网站伪造提交。
    public async Task<IActionResult> Register(RegisterViewModel model, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/"); // ??= 表示“为 null 才赋默认值”；~/ 表示首页。
        ViewData["ReturnUrl"] = returnUrl; // 验证失败回原页面时，仍保留目标地址。

        if (!ModelState.IsValid) // 检查 Required、EmailAddress 等规则收集的错误。
        {
            return View(model); // 带已填内容和错误重新显示表单。
        }

        if (!IsCampusEmail(model.Email)) // 辅助方法检查学校邮箱后缀。
        {
            // nameof 得到 "Email"，把错误显示在邮箱输入框旁边。
            ModelState.AddModelError(nameof(model.Email), $"Please use a {CampusEmailDomain} email address.");
            return View(model); // 停止注册，让用户修改邮箱。
        }

        // 把页面用的 RegisterViewModel 转成账号实体；密码不直接放进实体。
        var user = new AppUser
        {
            UserName = model.Email, // 邮箱同时作为登录用户名。
            Email = model.Email, // 保存邮箱。
            StudentId = model.StudentId, // 保存学号。
            Nickname = model.Nickname, // 保存昵称。
            PickupCampus = model.PickupCampus, // 保存常用校区。
            // 保留原项目行为：后缀合格就设为 true；这不代表发送过验证邮件。
            // 真正确认邮箱属于本人，需要后续增加邮件链接验证。
            IsSchoolVerified = true
        };

        // Identity 哈希密码，并通过 ApplicationDbContext 写入 AspNetUsers。
        var result = await _userManager.CreateAsync(user, model.Password);
        if (result.Succeeded) // 账号建立成功才继续加入角色。
        {
            var roleResult = await _userManager.AddToRoleAsync(user, UserRoles.Student); // 默认注册学生，不能从表单选 Admin。
            if (roleResult.Succeeded)
            {
                await _signInManager.SignInAsync(user, isPersistent: false); // 自动登录，不创建长期 Cookie。
                return LocalRedirect(returnUrl); // 只允许跳回本站地址。
            }

            await _userManager.DeleteAsync(user); // 加角色失败，删除刚建的半成品账号。
            AddErrors(roleResult); // 在表单显示角色错误。
        }
        else
        {
            AddErrors(result); // 显示邮箱重复、密码规则不满足等错误。
        }

        return View(model); // 有错误就回注册页。
    }

    [HttpGet] // 读取登录页面。
    [AllowAnonymous] // 尚未登录也能访问。
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl; // 保存登录后想返回的页面。
        return View(); // 返回 Views/Account/Login.cshtml。
    }

    [HttpPost] // 接收登录表单。
    [AllowAnonymous] // 未登录者可以提交登录资料。
    [ValidateAntiForgeryToken] // 检查防伪令牌。
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        returnUrl ??= Url.Content("~/"); // 没指定返回页面时回首页。
        ViewData["ReturnUrl"] = returnUrl; // 保留返回地址。

        if (ModelState.IsValid) // 先检查邮箱、密码是否按表单规则填写。
        {
            // Identity 对比密码哈希；RememberMe 决定 Cookie 是否长期保留。
            // lockoutOnFailure=true：失败计数可触发 Identity 的账号锁定规则。
            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl); // 登录成功回到本站目标页面。
            }

            ModelState.AddModelError(string.Empty, "Invalid login attempt."); // 统一错误，避免暴露邮箱是否注册。
        }

        return View(model); // 带错误重新显示登录页。
    }

    [Authorize] // 必须登录才能退出。
    [HttpPost] // 使用提交操作退出，不能靠普通链接触发。
    [ValidateAntiForgeryToken] // 防止其他网站强迫用户退出。
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync(); // 清除本站登录 Cookie。
        return RedirectToAction("Index", "Home"); // 回首页。
    }

    [AllowAnonymous] // 无权限提示本身允许任何人查看。
    public IActionResult AccessDenied()
    {
        return View(); // 返回 Views/Account/AccessDenied.cshtml。
    }

    // 把 Identity 返回的多个错误逐条转成 MVC 表单错误。
    private void AddErrors(IdentityResult result)
    {
        foreach (var error in result.Errors) // foreach 依次取出集合里的每一项。
        {
            ModelState.AddModelError(string.Empty, error.Description); // 空键表示表单整体错误。
        }
    }

    // static 表示方法只依赖传入邮箱和常量，不需要当前 Controller 的其他数据。
    private static bool IsCampusEmail(string email)
    {
        var atPosition = email.LastIndexOf('@'); // 找最后一个 @ 的位置。
        if (atPosition < 0) // 没有 @ 就不是学校邮箱。
        {
            return false;
        }

        var domain = email.Substring(atPosition + 1); // 从 @ 后取子字符串，得到邮箱域名。
        if (domain.Equals(CampusEmailDomain, StringComparison.OrdinalIgnoreCase)) // 比较时忽略大小写。
        {
            return true; // 与学校域名完全相同。
        }

        // 也允许 students.massey.ac.nz 等子域名；前面的点保证域名边界正确。
        return domain.EndsWith("." + CampusEmailDomain, StringComparison.OrdinalIgnoreCase);
    }
}
