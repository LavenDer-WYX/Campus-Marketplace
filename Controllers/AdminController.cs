// Authorize 可以限制只有符合 AdminOnly 策略的人进入后台。
using Microsoft.AspNetCore.Authorization;
// UserManager 用来取得处理举报的管理员编号。
using Microsoft.AspNetCore.Identity;
// 引入 MVC 控制器和返回结果类型。
using Microsoft.AspNetCore.Mvc;
// 引入 CountAsync、Include 等 EF Core 数据库方法。
using Microsoft.EntityFrameworkCore;
// 引入本项目的数据库上下文。
using WebApplication1.Data;
// 引入举报、通知和后台页面 ViewModel。
using WebApplication1.Models;

// 这个类属于 Controllers 分组。
namespace WebApplication1.Controllers;

// 这是服务器端权限检查；即使学生手动输入 /Admin 也无法进入。
[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{
    // 保存数据库访问对象。
    private readonly ApplicationDbContext _db;

    // 保存用户管理工具。
    private readonly UserManager<AppUser> _userManager;

    // ASP.NET 会自动提供这两个已在 Program.cs 注册的对象。
    public AdminController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        // 将数据库对象放入字段，供下面的方法使用。
        _db = db;
        // 将用户管理工具放入字段。
        _userManager = userManager;
    }

    // GET /Admin 显示统计首页，不修改数据。
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // 建立一个装统计数字的对象。
        var model = new AdminDashboardViewModel();
        // CountAsync 会让数据库计算数量，不需要把所有用户资料读入内存。
        model.UserCount = await _db.Users.CountAsync();
        // 统计所有仍然存在的商品。
        model.ListingCount = await _db.Listings.CountAsync();
        // 被隐藏、已预约、已卖出的商品都不算公开可交易商品。
        model.AvailableListingCount = await _db.Listings.CountAsync(item =>
            !item.IsHidden && item.IsAvailable && item.Status == "Available");
        // 只统计真正标记完成的交易请求。
        model.CompletedTradeCount = await _db.TradeRequests.CountAsync(request => request.Status == "Completed");
        // 统计待处理举报。
        model.PendingReportCount = await _db.Reports.CountAsync(report => report.Status == "Pending");
        // 把统计数字交给 Views/Admin/Index.cshtml。
        return View(model);
    }

    // GET /Admin/Reports 显示举报列表，可按状态筛选。
    [HttpGet]
    public async Task<IActionResult> Reports(string? status)
    {
        // 空状态默认看待处理；All 表示所有举报。
        if (string.IsNullOrWhiteSpace(status))
        {
            status = "Pending";
        }
        // 只接受页面支持的四种筛选选项。
        if (status != "Pending" && status != "Dismissed" && status != "ActionTaken" && status != "All")
        {
            return BadRequest();
        }

        // 这是只读列表，所以不需要 EF Core 跟踪实体变化。
        var query = _db.Reports.AsNoTracking();
        // All 不增加筛选条件；其他选项只读取相同状态的举报。
        if (status != "All")
        {
            query = query.Where(report => report.Status == status);
        }
        // 保存当前筛选选项，让页面可以显示出来。
        ViewData["Status"] = status;
        // 最新举报排在最前面，然后执行查询。
        var reports = await query.OrderByDescending(report => report.CreatedAt).ToListAsync();
        // 显示 Views/Admin/Reports.cshtml。
        return View(reports);
    }

    // GET /Admin/Details/5 显示一条举报及其商品信息。
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        // 读取举报，并加载对应商品和举报人；不读取密码等账号信息来显示。
        var report = await _db.Reports.AsNoTracking()
            .Include(item => item.Listing)
            .Include(item => item.Reporter)
            .FirstOrDefaultAsync(item => item.Id == id);
        // 查不到举报时返回 404。
        if (report == null)
        {
            return NotFound();
        }

        // 建立页面对象；把举报详情和处理表单放在一起。
        var model = new AdminReportActionViewModel();
        // 表单需要举报编号。
        model.Id = report.Id;
        // 页面需要显示数据库里的举报详情。
        model.Report = report;
        // 已经处理过的举报可以查看之前的说明。
        model.AdminNote = report.AdminNote;
        // 显示处理页面。
        return View(model);
    }

    // POST /Admin/Dismiss 表示管理员检查后决定不隐藏商品。
    [HttpPost]
    // 所有改变数据的操作都检查防伪令牌。
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss([Bind("Id,AdminNote")] AdminReportActionViewModel model)
    {
        // 将共同的处理步骤交给下面的方法；false 表示不隐藏商品。
        return await ResolveReportAsync(model, false);
    }

    // POST /Admin/HideListing 表示管理员确认违规并隐藏商品。
    [HttpPost]
    // 防止其他网站偷偷向这个地址提交表单。
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideListing([Bind("Id,AdminNote")] AdminReportActionViewModel model)
    {
        // true 表示本次处理还需要隐藏商品。
        return await ResolveReportAsync(model, true);
    }

    // 两种处理都要检查权限、读取举报、保存处理时间，集中放在这里避免复制两份。
    // private 表示浏览器不能把这个方法当作一个网页地址直接调用。
    private async Task<IActionResult> ResolveReportAsync(AdminReportActionViewModel model, bool hideListing)
    {
        // 读取当前管理员身份，用于处理记录。
        var adminId = _userManager.GetUserId(User);
        // 身份失效时重新登录。
        if (adminId == null)
        {
            return Challenge();
        }

        // 本次会修改举报和商品，因此不使用 AsNoTracking。
        var report = await _db.Reports
            .Include(item => item.Listing)
            .Include(item => item.Reporter)
            .FirstOrDefaultAsync(item => item.Id == model.Id);
        // 不能处理不存在的举报。
        if (report == null)
        {
            return NotFound();
        }
        // 已处理的举报不能再次处理，避免重复发通知或覆盖历史说明。
        if (report.Status != "Pending")
        {
            TempData["Message"] = "This report has already been reviewed.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        // 隐藏商品需要具体说明，让卖家和举报人知道处理原因。
        if (hideListing && string.IsNullOrWhiteSpace(model.AdminNote))
        {
            ModelState.AddModelError(nameof(model.AdminNote), "Explain why this item should be hidden.");
        }
        // 商品已经被删除时仍保留举报，但没有商品可以隐藏。
        if (hideListing && report.Listing == null)
        {
            ModelState.AddModelError(string.Empty, "The item was deleted. You can close this report without hiding it.");
        }
        // 说明太长、说明为空等问题需要让管理员修改后再提交。
        if (!ModelState.IsValid)
        {
            // 补回页面所需详情，浏览器提交的 Report 对象不会被信任。
            model.Report = report;
            return View("Details", model);
        }

        // 没有填写说明时保存 null；有说明时去掉首尾空白。
        report.AdminNote = model.AdminNote?.Trim();
        // 记录谁处理了举报。
        report.ResolvedById = adminId;
        // 记录服务器 UTC 处理时间。
        report.ResolvedAt = DateTime.UtcNow;

        // 隐藏商品只在管理员明确选择该操作时发生。
        if (hideListing && report.Listing != null)
        {
            // 保留原来的值，避免同一商品因不同举报被重复隐藏时反复通知卖家。
            var wasAlreadyHidden = report.Listing.IsHidden;
            // 独立的隐藏标记不能通过普通卖家的编辑表单取消。
            report.Listing.IsHidden = true;
            // 同时使商品不能再接受新的交易请求。
            report.Listing.IsAvailable = false;
            // 使用原有的 Unavailable 状态，兼容已有页面。
            report.Listing.Status = "Unavailable";
            // 记录商品状态修改时间。
            report.Listing.UpdatedAt = DateTime.UtcNow;
            // 记录本次举报已经采取措施。
            report.Status = "ActionTaken";

            // 第一次隐藏商品时，向卖家发送站内通知。
            if (!wasAlreadyHidden)
            {
                // 通知和举报使用同一个 DbContext，稍后统一保存。
                _db.Notifications.Add(new Notification
                {
                    // 卖家 Id 来自数据库商品，不能由网页随意指定。
                    UserId = report.Listing.SellerId,
                    // 通知类型说明这是管理员举报处理通知。
                    Type = "Report",
                    // 完整原因保存在举报记录中；通知只放简短提醒。
                    Content = "An administrator hid your item: " + report.Listing.Title + ". Please contact the project administrator for details.",
                    // 卖家可以回到自己的商品列表查看状态。
                    RelatedUrl = "/Listings/MyListings"
                });
            }
        }
        else
        {
            // 不隐藏商品时，只将这条举报关闭。
            report.Status = "Dismissed";
        }

        // 通知举报人处理结果；他可以在自己的举报历史查看管理员说明。
        _db.Notifications.Add(new Notification
        {
            // 只有提交这条举报的学生收到这条通知。
            UserId = report.ReporterId,
            // 保存通知分类。
            Type = "Report",
            // 使用标题快照，即使商品已经删除也有清晰提示。
            Content = "Your report about " + report.ListingTitle + " has been reviewed: " + report.Status + ".",
            // 这个页面只显示当前登录用户的举报。
            RelatedUrl = "/Reports/Index"
        });
        // 一次 SaveChanges 会把举报、商品及通知一起保存在一个数据库事务里。
        try
        {
            // DbContext 把 Status 配置为并发检查字段，只有原来的 Pending 状态没变才允许保存。
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            // 另一位管理员已经先处理了这条举报时，EF Core 会撤销本次事务里的全部修改。
            // 不覆盖对方的结果，也不重复保存通知；跳转后重新查询数据库的最终状态。
            TempData["Message"] = "Another administrator already reviewed this report. Please check the latest result.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }
        // 在跳转后的页面显示成功提示。
        TempData["Message"] = "The report has been reviewed.";
        // 返回这条举报的详情，以便管理员核对结果。
        return RedirectToAction(nameof(Details), new { id = report.Id });
    }
}
