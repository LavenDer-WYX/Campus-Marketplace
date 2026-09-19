using Microsoft.AspNetCore.Mvc; // 提供 Controller 和 View。
using System.Diagnostics; // 提供错误追踪编号 Activity。
using Microsoft.EntityFrameworkCore; // 提供 Include、ToListAsync 等数据库方法。
using Microsoft.AspNetCore.Identity; // 提供账号管理服务。
using WebApplication1.Data; // 引入 ApplicationDbContext。
using WebApplication1.Models; // 引入 AppUser 和 ErrorViewModel。

namespace WebApplication1.Controllers; // 把控制器归为同一组。

// 首页、隐私页和错误页由这个 Controller 处理。
public class HomeController : Controller
{
    private readonly ApplicationDbContext _db; // 通往数据库的入口。
    private readonly UserManager<AppUser> _userManager; // 用来读取当前用户 ID。

    public HomeController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db; // 保存框架传来的数据库服务。
        _userManager = userManager; // 保存框架传来的账号服务。
    }

    // 用户访问 / 或 /Home/Index 时显示最新六件商品。
    public async Task<IActionResult> Index()
    {
        var listings = await _db.Listings.AsNoTracking() // 只读数据，不跟踪修改。
            .Include(x => x.Category) // 同时查分类。
            .Include(x => x.Photos) // 同时查照片地址。
            .Where(x => x.IsAvailable && x.Status == "Available" && !x.IsHidden) // 排除售完或被隐藏的商品。
            .OrderByDescending(x => x.CreatedAt) // 最新商品排前面。
            .Take(6) // 首页最多六件，完整列表在 Listings/Index。
            .ToListAsync(); // 这里才执行 SQL，得到列表。

        var userId = _userManager.GetUserId(User); // 匿名用户得到 null。
        var favouriteIds = new HashSet<int>(); // 默认没有收藏；HashSet 方便快速判断某 ID 是否在集合里。
        if (userId != null) // 登录后才查个人收藏。
        {
            var savedIds = await _db.Favourites.AsNoTracking() // 只读取收藏。
                .Where(x => x.UserId == userId) // 只查自己的收藏。
                .Select(x => x.ListingId) // 页面只需要商品 ID。
                .ToListAsync(); // 执行查询。
            favouriteIds = savedIds.ToHashSet(); // 转为方便 Contains 判断的集合。
        }

        ViewBag.FavouriteIds = favouriteIds; // 页面据此显示收藏按钮状态。
        return View(listings); // 把商品交给 Views/Home/Index.cshtml。
    }

    public IActionResult Privacy()
    {
        return View(); // 显示静态隐私页。
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)] // 禁止缓存别的请求的错误页。
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier; // 优先用追踪 ID，没有就用 HTTP 请求 ID。
        var model = new ErrorViewModel { RequestId = requestId }; // 打包页面需要的错误编号。
        return View(model); // 返回错误页，开发人员可按编号查日志。
    }
}
