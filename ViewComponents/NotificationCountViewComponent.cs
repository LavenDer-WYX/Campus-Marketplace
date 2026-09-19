// ViewComponent 是可复用的小块页面逻辑，这里给导航栏查询未读数量。
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.ViewComponents;

public class NotificationCountViewComponent : ViewComponent
{
    // 保存数据库和用户服务。
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    // 框架自动传入服务，与 Controller 的构造函数相同。
    public NotificationCountViewComponent(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    // Layout 调用 Component.InvokeAsync 时运行这个方法。
    public async Task<IViewComponentResult> InvokeAsync()
    {
        // 从当前请求读取身份。
        var userId = _userManager.GetUserId(HttpContext.User);
        // 未登录时默认为零。
        int count = 0;
        // 只计算自己的未读通知。
        if (userId is not null)
        {
            count = await _db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);
        }
        // 将数量交给 Shared/Components/NotificationCount/Default.cshtml。
        return View(count);
    }
}
