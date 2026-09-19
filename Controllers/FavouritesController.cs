using Microsoft.AspNetCore.Authorization; // 提供 Authorize 权限检查。
using Microsoft.AspNetCore.Identity; // 提供账号管理服务。
using Microsoft.AspNetCore.Mvc; // 提供控制器和页面结果。
using Microsoft.EntityFrameworkCore; // 提供 EF Core 数据库查询方法。
using WebApplication1.Data; // 引入数据库上下文。
using WebApplication1.Models; // 引入 Favourite 等实体。

namespace WebApplication1.Controllers; // 为控制器分组。

[Authorize] // 整个收藏功能都要求先登录。
public class FavouritesController : Controller
{
    private readonly ApplicationDbContext _db; // 查询和保存收藏记录。
    private readonly UserManager<AppUser> _userManager; // 读取当前登录人 ID。

    public FavouritesController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db; // 保存数据库服务。
        _userManager = userManager; // 保存账号服务。
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User); // 从身份信息取用户 ID，不接受网页传来的别人的 ID。
        var favourites = await _db.Favourites.AsNoTracking() // 只读收藏列表。
            .Include(x => x.Listing).ThenInclude(x => x!.Category) // 收藏 → 商品 → 分类。
            .Include(x => x.Listing).ThenInclude(x => x!.Photos) // 收藏 → 商品 → 照片。
            .Where(x => x.UserId == userId && x.Listing != null && !x.Listing.IsHidden) // 只显示自己的收藏，隐藏违规商品。
            .OrderByDescending(x => x.CreatedAt) // 最新收藏排在前面。
            .ToListAsync(); // 执行 SQL 并转换为列表。

        return View(favourites); // 把列表交给 Views/Favourites/Index.cshtml。
    }

    [HttpPost] // 添加/取消收藏会改数据库，必须用 POST。
    [ValidateAntiForgeryToken] // 只接受有正确本站表单令牌的提交。
    public async Task<IActionResult> Toggle(int listingId, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User); // 读取当前用户。
        if (userId == null)
        {
            return Challenge(); // 没有有效身份就要求登录。
        }

        var listing = await _db.Listings.FindAsync(listingId); // 先确认商品存在。
        if (listing == null)
        {
            return NotFound(); // 不能收藏已经被删除的商品。
        }

        var favourite = await _db.Favourites.FindAsync(userId, listingId); // 收藏的联合主键是用户 ID + 商品 ID。
        if (favourite == null) // 当前未收藏，就准备新增。
        {
            if (listing.IsHidden)
            {
                return NotFound(); // 隐藏商品不可通过手动提交 ID 加入收藏。
            }

            _db.Favourites.Add(new Favourite { UserId = userId, ListingId = listingId }); // 此处只是让 EF 记录“待插入”。
            TempData["Message"] = "Added to favourites."; // TempData 会在下一次跳转后显示一次提示。
        }
        else
        {
            _db.Favourites.Remove(favourite); // 已收藏则取消；即使商品隐藏，也允许删除自己的旧收藏。
            TempData["Message"] = "Removed from favourites."; // 设置操作结果提示。
        }

        await _db.SaveChangesAsync(); // 这里才真正向 SQL Server 提交新增/删除。

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)) // 返回地址必须是本站 URL。
        {
            return LocalRedirect(returnUrl); // 通常返回用户原本所在的商品列表。
        }

        if (listing.IsHidden)
        {
            return RedirectToAction(nameof(Index)); // 取消隐藏商品收藏后回收藏列表，避免跳进不可见详情页。
        }

        return RedirectToAction("Details", "Listings", new { id = listingId }); // 没提供返回地址就显示商品详情。
    }
}
