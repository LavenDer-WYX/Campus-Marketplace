// Authorize 用来要求用户先登录。
using Microsoft.AspNetCore.Authorization;
// UserManager 用来取得当前登录用户的 Id。
using Microsoft.AspNetCore.Identity;
// Controller、IActionResult 等 MVC 功能来自这个命名空间。
using Microsoft.AspNetCore.Mvc;
// 引入数据库异步查询方法，例如 FirstOrDefaultAsync。
using Microsoft.EntityFrameworkCore;
// 引入本项目的数据库上下文。
using WebApplication1.Data;
// 引入 Report 和举报表单 ViewModel。
using WebApplication1.Models;

// 把这个类归入项目的 Controllers 分组。
namespace WebApplication1.Controllers;

// 此控制器的所有页面都要求登录，匿名用户会被引导到登录页面。
[Authorize]
public class ReportsController : Controller
{
    // 保存数据库访问入口，供下面每个方法查询或保存数据。
    private readonly ApplicationDbContext _db;

    // 保存 Identity 的用户管理工具，避免相信浏览器传来的用户编号。
    private readonly UserManager<AppUser> _userManager;

    // ASP.NET 创建这个 Controller 时，会自动把所需的两个对象传进来。
    public ReportsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        // 将传入的数据库对象保存在当前 Controller 中。
        _db = db;
        // 将传入的用户管理对象保存下来。
        _userManager = userManager;
    }

    // GET /Reports 显示当前用户自己的举报历史。
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        // 从登录 Cookie 对应的身份中取得用户编号，而不是使用网址参数。
        var userId = _userManager.GetUserId(User);
        // 如果身份信息失效，就重新要求登录。
        if (userId == null)
        {
            return Challenge();
        }

        // 只读取当前用户的举报，按最新时间排在前面；AsNoTracking 表示只读查询。
        var reports = await _db.Reports.AsNoTracking()
            .Where(report => report.ReporterId == userId)
            .OrderByDescending(report => report.CreatedAt)
            .ToListAsync();
        // 把查询结果交给 Views/Reports/Index.cshtml。
        return View(reports);
    }

    // GET /Reports/Create?listingId=5 显示举报编号为 5 的商品的表单。
    [HttpGet]
    public async Task<IActionResult> Create(int listingId)
    {
        // 取得当前用户编号，下面要用它检查是否在举报自己的商品。
        var userId = _userManager.GetUserId(User);
        // 无法取得用户身份时，要求重新登录。
        if (userId == null)
        {
            return Challenge();
        }

        // 根据编号读取商品；没有找到时 listing 的值是 null。
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == listingId);
        // 不允许举报不存在或已经被管理员隐藏的商品。
        if (listing == null || listing.IsHidden)
        {
            return NotFound();
        }
        // 用户不能举报自己发布的商品。
        if (listing.SellerId == userId)
        {
            return Forbid();
        }

        // 同一用户对同一商品有待处理举报时，直接让他查看原来的举报。
        if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
            report.ListingId == listingId && report.Status == "Pending"))
        {
            // TempData 会把提示保留到下一次跳转后的页面。
            TempData["Message"] = "You already have a pending report for this item.";
            // nameof(Index) 相当于写字符串 "Index"，改方法名时更不容易遗漏。
            return RedirectToAction(nameof(Index));
        }

        // 创建一个仅包含表单所需信息的对象。
        var model = new ReportCreateViewModel();
        // 商品编号来自查询结果。
        model.ListingId = listing.Id;
        // 商品标题也来自数据库，用户不能伪造标题。
        model.ListingTitle = listing.Title;
        // 显示 Views/Reports/Create.cshtml。
        return View(model);
    }

    // POST /Reports/Create 接收学生提交的举报表单。
    [HttpPost]
    // 检查表单的防伪令牌，避免其他网站冒用用户身份提交举报。
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ListingId,Reason")] ReportCreateViewModel model)
    {
        // 取得真实登录用户，不接收表单中的 ReporterId。
        var userId = _userManager.GetUserId(User);
        // 不能识别身份时要求重新登录。
        if (userId == null)
        {
            return Challenge();
        }

        // POST 必须再次查询商品，因为显示表单之后商品可能已经改变。
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.ListingId);
        // 商品不存在或被隐藏时停止提交。
        if (listing == null || listing.IsHidden)
        {
            return NotFound();
        }
        // 不能通过修改隐藏输入框来举报自己的商品。
        if (listing.SellerId == userId)
        {
            return Forbid();
        }
        // 验证失败需要重新显示表单，所以补回服务器读取的标题。
        model.ListingTitle = listing.Title;
        // Required 和 StringLength 等验证没有通过时，不往数据库写入数据。
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // 再次检查重复举报，防止重复点击或同时打开多个表单。
        if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
            report.ListingId == model.ListingId && report.Status == "Pending"))
        {
            // 在表单顶部显示错误，而不是创建第二条举报。
            ModelState.AddModelError(string.Empty, "You already have a pending report for this item.");
            return View(model);
        }

        // 创建将被保存到数据库的实体。
        var newReport = new Report();
        // 举报人由服务器决定。
        newReport.ReporterId = userId;
        // 记录已经检查过的商品编号。
        newReport.ListingId = listing.Id;
        // 保存商品标题快照，让举报历史不依赖商品一直存在。
        newReport.ListingTitle = listing.Title;
        // Trim 去掉用户输入内容前后的空格。
        newReport.Reason = model.Reason.Trim();
        // 新举报只能是待处理状态，不能由用户选择已处理。
        newReport.Status = "Pending";
        // 保存服务器当前 UTC 时间。
        newReport.CreatedAt = DateTime.UtcNow;
        // Add 先让 EF Core 跟踪这条新记录，还没有真正执行 SQL。
        _db.Reports.Add(newReport);

        // 数据库还有唯一索引处理极短时间内同时提交造成的重复。
        try
        {
            // 真正执行 INSERT，把举报保存到数据库。
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // 保存失败后取消对这条未保存实体的跟踪，避免稍后误保存。
            _db.Entry(newReport).State = EntityState.Detached;
            // 只把确认属于重复举报的错误转换成表单提示。
            if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
                report.ListingId == model.ListingId && report.Status == "Pending"))
            {
                ModelState.AddModelError(string.Empty, "You already have a pending report for this item.");
                return View(model);
            }
            // 其他数据库故障交给应用的统一错误处理，避免伪装成提交成功。
            throw;
        }

        // 告诉用户举报已提交，但还需要管理员判断。
        TempData["Message"] = "Your report has been submitted for review.";
        // 使用跳转避免用户刷新页面时重复发送 POST。
        return RedirectToAction(nameof(Index));
    }
}
