using Microsoft.AspNetCore.Authorization; // 提供登录检查。
using Microsoft.AspNetCore.Identity; // 提供 UserManager。
using Microsoft.AspNetCore.Mvc; // 提供 Controller 和 View。
using Microsoft.EntityFrameworkCore; // 提供数据库查询方法。
using WebApplication1.Data; // 引入 DbContext。
using WebApplication1.Models; // 引入账号和页面模型。

namespace WebApplication1.Controllers; // 控制器命名空间。

[Authorize] // 个人资料页面只能由已登录用户访问。
public class ProfileController : Controller
{
    private readonly UserManager<AppUser> _userManager; // 读取当前账号资料。
    private readonly ApplicationDbContext _db; // 查询当前账号发布的商品。

    public ProfileController(UserManager<AppUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager; // 保存账号服务。
        _db = db; // 保存数据库服务。
    }

    // 当前页面仅查看个人资料，暂不修改头像和昵称。
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User); // 按 Cookie 的身份查询完整 AppUser。
        if (user == null) // 例如 Cookie 还在，但数据库账号已被删除。
        {
            return Challenge(); // 要求重新登录。
        }

        var listings = await _db.Listings.AsNoTracking() // 只展示，不跟踪修改。
            .Include(x => x.Category) // 页面需要关联的分类名称。
            .Where(x => x.SellerId == user.Id) // 只显示自己的商品，保留自己隐藏商品的管理记录。
            .OrderByDescending(x => x.CreatedAt) // 最新发布的排前面。
            .ToListAsync(); // 执行数据库查询。

        var model = new ProfileViewModel { User = user, Listings = listings }; // 打包“一个用户”和“多件商品”。
        return View(model); // 返回 Views/Profile/Index.cshtml。
    }
}
