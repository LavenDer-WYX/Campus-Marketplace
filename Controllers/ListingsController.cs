// Authorize 提供登录限制，Identity 用来读取当前账号。
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
// MVC 提供 Controller、View、表单绑定和下拉列表。
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
// EF Core 提供 Include、Where、ToListAsync 等数据库方法。
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

// 负责商品搜索、详情、创建、编辑、删除和“我的商品”。
public class ListingsController : Controller
{
    // DbContext 是 C# 和 SQL Server 之间的入口。
    private readonly ApplicationDbContext _db;
    // UserManager 从已经验证的登录身份中读取用户 ID 和角色。
    private readonly UserManager<AppUser> _userManager;
    // IWebHostEnvironment 告诉我们 wwwroot 的真实磁盘路径。
    private readonly IWebHostEnvironment _environment;

    // ASP.NET 按 Program.cs 的注册自动传入这三个服务。
    public ListingsController(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        IWebHostEnvironment environment)
    {
        // 把服务保存到字段，下面每个方法都能使用。
        _db = db;
        _userManager = userManager;
        _environment = environment;
    }

    // GET /Listings：显示所有公开、可交易的商品，也接收搜索条件。
    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? categoryId, string? condition, string? listingType)
    {
        // IQueryable 会先记录查询条件，到 ToListAsync 时才真正发送 SQL。
        var query = _db.Listings.AsNoTracking()
            // 商品卡片需要分类名称和照片地址，所以一起查询。
            .Include(x => x.Category)
            .Include(x => x.Photos)
            // 只公开仍可交易、状态正确、没有被管理员隐藏的商品。
            .Where(x => x.IsAvailable && x.Status == "Available" && !x.IsHidden);

        // 有关键词时，在标题或描述中搜索。
        if (!string.IsNullOrWhiteSpace(search))
        {
            // Trim 去掉用户不小心输入在两端的空格。
            search = search.Trim();
            query = query.Where(x => x.Title.Contains(search) || x.Description.Contains(search));
        }
        // 选择分类时只保留该分类。
        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }
        // 选择成色时使用完全匹配。
        if (!string.IsNullOrWhiteSpace(condition))
        {
            query = query.Where(x => x.Condition == condition);
        }
        // 选择 Sale、Swap 或 Giveaway 时使用完全匹配。
        if (!string.IsNullOrWhiteSpace(listingType))
        {
            query = query.Where(x => x.ListingType == listingType);
        }

        // 页面下拉框需要真实分类数据，并保留当前选项。
        await SetCategoryOptionsAsync(categoryId);
        // ViewData 把搜索条件交还页面，让输入框显示当前值。
        ViewData["Search"] = search;
        ViewData["Condition"] = condition;
        ViewData["ListingType"] = listingType;

        // 匿名用户没有 ID，也没有收藏状态。
        var userId = _userManager.GetUserId(User);
        var favouriteIds = new HashSet<int>();
        // 登录后只查询自己的收藏商品编号。
        if (userId is not null)
        {
            var ids = await _db.Favourites.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ListingId)
                .ToListAsync();
            // HashSet 的 Contains 查找很快，适合页面逐张卡片判断爱心状态。
            favouriteIds = ids.ToHashSet();
        }
        ViewBag.FavouriteIds = favouriteIds;

        // 最新发布的商品排前面；这一行真正执行数据库查询。
        var listings = await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View(listings);
    }

    // GET /Listings/Details/5：显示指定商品详情。
    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        // 详情页需要商品、分类、卖家和照片。
        var listing = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Seller)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id);
        // 不存在的编号返回 404。
        if (listing is null) return NotFound();

        // 判断查看者身份；匿名用户的 userId 是 null。
        var userId = _userManager.GetUserId(User);
        bool isOwner = userId == listing.SellerId;
        bool isAdmin = User.IsInRole(UserRoles.Admin);
        // 被管理员隐藏后，只让卖家和管理员看到状态说明。
        if (listing.IsHidden && !isOwner && !isAdmin) return NotFound();

        // ViewBag 是只给当前页面使用的少量附加数据。
        ViewBag.IsOwner = isOwner;
        ViewBag.IsFavourite = userId is not null && await _db.Favourites
            .AnyAsync(x => x.UserId == userId && x.ListingId == id);
        ViewBag.HasPendingRequest = userId is not null && await _db.TradeRequests
            .AnyAsync(x => x.ListingId == id && x.BuyerId == userId && x.Status == "Pending");
        return View(listing);
    }

    // GET /Listings/Create：只有登录用户能打开发布页。
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        // 从数据库建立分类下拉列表。
        await SetCategoryOptionsAsync();
        // 新对象带有模型中定义的默认状态。
        return View(new Listing());
    }

    // POST /Listings/Create：接收发布商品表单和图片。
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        // Bind 明确允许网页提交哪些字段，防止网页伪造 SellerId、Status、IsHidden。
        [Bind("Title,CategoryId,Description,Condition,Price,ListingType,PickupLocation,IsAvailable")] Listing listing,
        List<IFormFile>? photos)
    {
        // SellerId 必填，但它稍后从登录身份设置，因此先移除表单绑定错误。
        ModelState.Remove(nameof(Listing.SellerId));
        // 检查照片数量、大小、扩展名和浏览器声明的类型。
        ValidatePhotos(photos, 0);
        // 数据库外键要求分类真实存在，不能只检查编号大于零。
        if (!await _db.Categories.AnyAsync(x => x.Id == listing.CategoryId))
        {
            ModelState.AddModelError(nameof(Listing.CategoryId), "Please choose an existing category.");
        }
        // 出售商品必须填写价格。
        if (listing.ListingType == "Sale" && listing.Price is null)
        {
            ModelState.AddModelError(nameof(Listing.Price), "A price is required for sale listings.");
        }
        // 换物和赠送不保存无意义的价格。
        else if (listing.ListingType != "Sale")
        {
            listing.Price = null;
        }

        // 任何验证错误都会回到表单，数据库不会写入半成品。
        if (!ModelState.IsValid)
        {
            await SetCategoryOptionsAsync(listing.CategoryId);
            return View(listing);
        }

        // 卖家身份来自登录 Cookie，不能由隐藏输入框决定。
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();
        listing.SellerId = userId;
        // 新商品由服务器设置初始状态和时间。
        listing.Status = "Available";
        listing.IsHidden = false;
        listing.CreatedAt = DateTime.UtcNow;
        listing.UpdatedAt = DateTime.UtcNow;
        // 先保存商品，数据库才会产生 listing.Id，照片记录需要这个编号。
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        // 图片文件保存到 wwwroot，图片 URL 保存到 ListingPhotos 表。
        await SavePhotosAsync(listing.Id, photos);
        return RedirectToAction(nameof(Details), new { id = listing.Id });
    }

    // GET /Listings/Edit/5：读取原商品并显示编辑页。
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        // 只有商品主人或管理员可以管理。
        if (!CanManage(listing)) return Forbid();
        await SetCategoryOptionsAsync(listing.CategoryId);
        return View(listing);
    }

    // POST /Listings/Edit/5：保存允许修改的字段和新增照片。
    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Title,CategoryId,Description,Condition,Price,ListingType,PickupLocation,Status,IsAvailable")] Listing input,
        List<IFormFile>? photos)
    {
        // 从数据库读取旧实体和已有照片；不能直接相信浏览器传来的整件对象。
        var listing = await _db.Listings.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();

        // SellerId 仍然来自旧数据库记录，不参加页面验证。
        ModelState.Remove(nameof(Listing.SellerId));
        ValidatePhotos(photos, listing.Photos.Count);
        // 分类必须真实存在。
        if (!await _db.Categories.AnyAsync(x => x.Id == input.CategoryId))
        {
            ModelState.AddModelError(nameof(Listing.CategoryId), "Please choose an existing category.");
        }
        if (input.ListingType == "Sale" && input.Price is null)
        {
            ModelState.AddModelError(nameof(Listing.Price), "A price is required for sale listings.");
        }
        else if (input.ListingType != "Sale")
        {
            input.Price = null;
        }

        if (!ModelState.IsValid)
        {
            // 返回页面需要商品编号；隐藏状态只显示服务器旧值。
            input.Id = id;
            input.IsHidden = listing.IsHidden;
            await SetCategoryOptionsAsync(input.CategoryId);
            return View(input);
        }

        // 这些内容字段允许主人修改，SellerId、CreatedAt、IsHidden 都不会被覆盖。
        listing.Title = input.Title;
        listing.CategoryId = input.CategoryId;
        listing.Description = input.Description;
        listing.Condition = input.Condition;
        listing.Price = input.Price;
        listing.ListingType = input.ListingType;
        listing.PickupLocation = input.PickupLocation;

        // 已接受或完成的交易不能通过普通商品编辑重新开放。
        bool hasActiveTrade = await _db.TradeRequests.AnyAsync(x => x.ListingId == id &&
            (x.Status == "Accepted" || x.Status == "Completed"));
        if (listing.IsHidden)
        {
            // 管理员下架拥有最高优先级，卖家不能自行取消。
            listing.Status = "Unavailable";
            listing.IsAvailable = false;
        }
        else if (!hasActiveTrade)
        {
            // 没有已接受交易时才允许按表单调整商品状态。
            listing.Status = input.Status;
            // 非 Available 状态必须从公开可交易列表移除。
            listing.IsAvailable = input.Status == "Available" && input.IsAvailable;
        }
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await SavePhotosAsync(listing.Id, photos);
        return RedirectToAction(nameof(Details), new { id });
    }

    // GET /Listings/Delete/5：只显示确认页面，不立即删除。
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();
        return View(listing);
    }

    // 页面仍提交到 Delete，但实际 C# 方法名用 DeleteConfirmed 区分 GET 与 POST。
    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        // Include Photos 是为了同时删除真实图片文件。
        var listing = await _db.Listings.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();
        // 先删磁盘文件，再由 EF 删除商品和级联数据。
        DeletePhotoFiles(listing.Photos);
        _db.Listings.Remove(listing);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(MyListings));
    }

    // GET /Listings/MyListings：显示当前用户发布过的全部商品，包括隐藏记录。
    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyListings()
    {
        var userId = _userManager.GetUserId(User);
        var listings = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.SellerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(listings);
    }

    // private 辅助方法不会成为网页地址，只供本控制器复用。
    private bool CanManage(Listing listing)
    {
        // 商品主人或 Admin 角色可以管理。
        return listing.SellerId == _userManager.GetUserId(User) || User.IsInRole(UserRoles.Admin);
    }

    // 建立分类下拉列表；selectedId 让编辑页保留当前分类。
    private async Task SetCategoryOptionsAsync(int? selectedId = null)
    {
        var categories = await _db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();
        // SelectList 告诉 Razor：值用 Id，显示文字用 Name。
        ViewBag.Categories = new SelectList(categories, nameof(Category.Id), nameof(Category.Name), selectedId);
    }

    // 在写磁盘前验证用户上传的照片。
    private void ValidatePhotos(List<IFormFile>? photos, int currentPhotoCount)
    {
        // 没传照片就不需要检查。
        if (photos is null || photos.Count == 0) return;
        // 一件商品新旧照片合计最多五张。
        if (currentPhotoCount + photos.Count > 5)
        {
            ModelState.AddModelError("photos", "Each listing can have up to 5 photos.");
        }
        // 同时检查扩展名和浏览器声明的 MIME 类型；这是基础检查，不是图片病毒扫描。
        string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        string[] allowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
        foreach (var photo in photos)
        {
            string extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(photo.ContentType))
            {
                ModelState.AddModelError("photos", "Only JPG, PNG and WebP images are allowed.");
            }
            // 空文件和超过 5 MB 的文件都拒绝。
            if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("photos", "Each photo must be smaller than 5 MB.");
            }
        }
    }

    // 保存真实文件，并向 ListingPhotos 表加入可访问的 URL。
    private async Task SavePhotosAsync(int listingId, List<IFormFile>? photos)
    {
        if (photos is null || photos.Count == 0) return;
        // Path.Combine 会使用 Windows 正确的路径分隔符。
        string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        // 文件夹不存在就建立，已经存在时不会报错。
        Directory.CreateDirectory(uploadFolder);
        foreach (var photo in photos)
        {
            string extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            // 随机文件名避免同名覆盖，也不直接使用用户提供的原文件名。
            string fileName = Guid.NewGuid().ToString("N") + extension;
            string filePath = Path.Combine(uploadFolder, fileName);
            // await using 会在复制结束后关闭文件流。
            await using var stream = System.IO.File.Create(filePath);
            await photo.CopyToAsync(stream);
            _db.ListingPhotos.Add(new ListingPhoto
            {
                ListingId = listingId,
                ImageUrl = "/uploads/listings/" + fileName
            });
        }
        await _db.SaveChangesAsync();
    }

    // 删除商品时清理它在磁盘上的照片。
    private void DeletePhotoFiles(IEnumerable<ListingPhoto> photos)
    {
        string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        foreach (var photo in photos)
        {
            // GetFileName 丢掉 URL 中可能出现的目录部分，只保留安全文件名。
            string fileName = Path.GetFileName(photo.ImageUrl);
            string filePath = Path.Combine(uploadFolder, fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
