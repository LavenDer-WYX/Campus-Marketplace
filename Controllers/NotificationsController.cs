// Authorize 限制登录访问，Mvc 提供 Controller 和 View。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
// EF Core 提供异步查询和批量更新方法。
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// 整个控制器都要求先登录。
[Authorize]
public class NotificationsController : Controller
{
    // 当前请求的数据库对象。
    private readonly ApplicationDbContext _db;
    // 获取当前登录用户，不能相信表单传来的 UserId。
    private readonly UserManager<AppUser> _userManager;

    // ASP.NET 自动传入 Program.cs 中注册过的服务。
    public NotificationsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        // 保存引用，下面的 Action 都可以使用。
        _db = db;
        _userManager = userManager;
    }

    // 显示自己的通知，一次最多 30 条。
    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {
        // 限制页码，避免负数和过大数值。
        page = Math.Clamp(page, 1, 10000);
        // 身份来自已验证的登录 Cookie。
        var userId = _userManager.GetUserId(User);
        // 没有身份时重新要求登录。
        if (userId is null) return Challenge();
        // 只读查询自己的记录，多取一条判断是否有下一页。
        var notifications = await _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * 30)
            .Take(31)
            .ToListAsync();
        // 传给页面，用于显示翻页按钮。
        ViewBag.HasNext = notifications.Count > 30;
        ViewBag.Page = page;
        // 打开列表不自动已读，用户可手动标记。
        return View(notifications.Take(30).ToList());
    }

    // 修改数据用 POST，同时校验表单防伪令牌。
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {
        // 从登录信息获取身份。
        var userId = _userManager.GetUserId(User);
        // 同时检查 ID 和所有者，防止修改别人的通知。
        var notification = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
        // 不存在和不属于自己统一返回 404。
        if (notification is null) return NotFound();
        // 修改对象，再提交到数据库。
        notification.IsRead = true;
        await _db.SaveChangesAsync();
        // 跳转回列表，刷新页面不会重复提交。
        return RedirectToAction(nameof(Index));
    }

    // 一次将当前用户的未读通知全部标记为已读。
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {
        // 当前用户编号来自登录身份。
        var userId = _userManager.GetUserId(User);
        // 直接执行 SQL UPDATE，不必把所有通知先搬到内存中。
        await _db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true));
        // ExecuteUpdateAsync 已执行 SQL，不需要再次 SaveChangesAsync。
        return RedirectToAction(nameof(Index));
    }
}
