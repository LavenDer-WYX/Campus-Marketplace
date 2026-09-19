using System.Data; // 提供数据库事务的隔离级别 IsolationLevel。
using Microsoft.Data.SqlClient; // 用于识别 SQL Server 同时操作时可能出现的死锁错误。
using Microsoft.AspNetCore.Authorization; // 提供登录权限检查。
using Microsoft.AspNetCore.Identity; // 提供当前用户 ID。
using Microsoft.AspNetCore.Mvc; // 提供 Controller、View 和跳转。
using Microsoft.EntityFrameworkCore; // 提供数据库查询、保存和事务方法。
using WebApplication1.Data; // 引入数据库上下文。
using WebApplication1.Models; // 引入交易请求和通知模型。

namespace WebApplication1.Controllers; // 控制器命名空间。

[Authorize] // 未登录的人不能发起或处理交易。
public class TradeRequestsController : Controller
{
    private readonly ApplicationDbContext _db; // 交易、商品、通知都保存在同一个数据库上下文中。
    private readonly UserManager<AppUser> _userManager; // 读取登录用户，不信任表单里的买卖家身份。

    public TradeRequestsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db; // 保存数据库服务。
        _userManager = userManager; // 保存账号服务。
    }

    // 查询自己作为买家或卖家的全部交易请求。
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User); // 当前登录人。
        var requests = await _db.TradeRequests.AsNoTracking() // 页面只展示，不修改。
            .Include(x => x.Listing) // 一起查询商品。
            .Include(x => x.Buyer) // 一起查询买家。
            .Include(x => x.Seller) // 一起查询卖家。
            .Where(x => x.BuyerId == userId || x.SellerId == userId) // 不能读取与自己无关的交易。
            .OrderByDescending(x => x.CreatedAt) // 最新请求排前面。
            .ToListAsync(); // 执行 SQL 查询。

        return View(requests); // 把结果交给 Views/TradeRequests/Index.cshtml。
    }

    [HttpPost] // 建立请求会写数据库。
    [ValidateAntiForgeryToken] // 验证提交来自本站表单。
    public async Task<IActionResult> Create(int listingId, string? note)
    {
        var userId = _userManager.GetUserId(User); // 买家是当前登录人。
        if (userId == null)
        {
            return Challenge(); // 没有有效身份就要求登录。
        }

        var listing = await _db.Listings.FindAsync(listingId); // 查询真实商品资料。
        if (listing == null || listing.IsHidden)
        {
            return NotFound(); // 删除或被隐藏的商品都不能再发起交易。
        }
        if (listing.SellerId == userId)
        {
            TempData["Message"] = "You cannot request your own item."; // 提示不能买自己的商品。
            return RedirectToAction("Details", "Listings", new { id = listingId }); // 返回商品页面。
        }
        if (!listing.IsAvailable || listing.Status != "Available") // 两个状态都必须显示可交易。
        {
            TempData["Message"] = "This item is not available now."; // 商品可能已经被预订或卖掉。
            return RedirectToAction("Details", "Listings", new { id = listingId }); // 返回查看最新状态。
        }
        if (listing.ListingType == "Sale")
        {
            // Sale 已改为专门的 BuyNow 流程，不能再从旧网址偷偷建立等待卖家批准的请求。
            TempData["Message"] = "Sale listings use Buy now. Please click Buy now on the item page.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var alreadyPending = await _db.TradeRequests.AnyAsync(x =>
            x.ListingId == listingId && x.BuyerId == userId && x.Status == "Pending"); // 查自己是否已有待处理请求。
        if (alreadyPending)
        {
            TempData["Message"] = "You already have a pending request for this item."; // 避免反复点击产生普通重复请求。
            return RedirectToAction("Details", "Listings", new { id = listingId }); // 返回商品页。
        }

        if (string.IsNullOrWhiteSpace(note)) // 全空格和没填写都按无备注处理。
        {
            note = null;
        }
        else
        {
            note = note.Trim(); // 去掉备注两端空格。
        }
        if (note != null && note.Length > 500) // 防止绕过前端提交过长备注。
        {
            TempData["Message"] = "The note must be 500 characters or shorter."; // 告诉用户长度要求。
            return RedirectToAction("Details", "Listings", new { id = listingId }); // 返回重新填写。
        }

        var requestType = listing.ListingType; // 这里只会是 Swap 或 Giveaway；Sale 由 BuyNow 处理。
        _db.TradeRequests.Add(new TradeRequest
        {
            ListingId = listingId, // 申请哪件商品。
            BuyerId = userId, // 谁提交请求。
            SellerId = listing.SellerId, // 卖家由商品决定，不能由表单随便指定。
            RequestType = requestType, // Buy、Swap 或 Giveaway。
            Note = note // 买家备注，初始状态和创建时间由模型默认值提供。
        });
        AddTradeNotification(listing.SellerId, "You received a new trade request."); // 同时准备通知卖家。
        await _db.SaveChangesAsync(); // 请求和通知同一次提交，任何一项失败都会一起回滚。

        TempData["Message"] = "Trade request sent."; // 提示提交成功。
        return RedirectToAction(nameof(Index)); // 跳转自己的交易列表。
    }

    // Sale 商品使用这个方法实现“直接购买”。
    // 与 Create 不同，这里不会建立 Pending 请求，也不需要卖家再点 Accept。
    [HttpPost] // 点击 Buy now 会立即修改商品和交易状态。
    [ValidateAntiForgeryToken] // 防止其他网站冒充买家提交购买操作。
    public async Task<IActionResult> BuyNow(int listingId, string? note)
    {
        var userId = _userManager.GetUserId(User); // 买家永远取当前登录账号，不能由网页自己传入。
        if (userId == null)
        {
            return Challenge(); // 登录信息失效时重新要求登录。
        }

        // Serializable 事务让“检查还有货”和“立即锁定商品”成为一个不可分开的步骤。
        // 如果两个人同时点击，数据库会让他们排队，只有第一个人能看到 Available。
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == listingId); // 在事务中重新读取商品最新状态。
            if (listing == null || listing.IsHidden)
            {
                return NotFound(); // 不购买不存在或被管理员隐藏的商品。
            }
            if (listing.SellerId == userId)
            {
                TempData["Message"] = "You cannot buy your own item."; // 卖家不能购买自己的商品。
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }
            if (listing.ListingType != "Sale")
            {
                TempData["Message"] = "Buy now is only available for sale listings."; // 交换和赠送仍使用普通请求。
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }
            if (!listing.IsAvailable || listing.Status != "Available")
            {
                TempData["Message"] = "Sorry, another buyer has already reserved this item."; // 后到的买家不会重复买到同一商品。
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                note = null; // 没写备注时数据库保存 null。
            }
            else
            {
                note = note.Trim(); // 去掉备注前后的空格。
            }
            if (note != null && note.Length > 500)
            {
                TempData["Message"] = "The note must be 500 characters or shorter."; // 后端再次检查，不能只相信 HTML maxlength。
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }

            var purchase = new TradeRequest
            {
                ListingId = listing.Id, // 买的是当前商品。
                BuyerId = userId, // 当前登录人是买家。
                SellerId = listing.SellerId, // 卖家来自商品记录。
                RequestType = "Buy", // 这是购买，不是交换或赠送。
                Note = note, // 可选的取货备注。
                Status = "Accepted" // 关键点：直接进入 Accepted，不等待卖家同意。
            };
            _db.TradeRequests.Add(purchase); // 准备新增购买记录。

            listing.Status = "Reserved"; // 商品立即为当前买家保留。
            listing.IsAvailable = false; // 首页和商品列表不再允许其他人购买。
            listing.UpdatedAt = DateTime.UtcNow; // 保存锁定时间。

            // 商品以前可能已经收到普通 Pending 请求；直接购买成功后，这些请求都不能再被接受。
            var oldRequests = await _db.TradeRequests.Where(x =>
                x.ListingId == listing.Id && x.Status == "Pending").ToListAsync();
            foreach (var oldRequest in oldRequests)
            {
                oldRequest.Status = "Rejected"; // 自动拒绝旧请求。
                oldRequest.UpdatedAt = DateTime.UtcNow; // 记录处理时间。
                AddTradeNotification(oldRequest.BuyerId,
                    "Your request was declined because another buyer used Buy now."); // 通知旧请求的买家。
            }

            AddTradeNotification(listing.SellerId,
                $"Your item '{listing.Title}' was reserved through Buy now. Please arrange pickup with the buyer."); // 告知卖家，无需卖家再确认。

            await _db.SaveChangesAsync(); // 购买记录、商品状态、旧请求和通知一起保存。
            await transaction.CommitAsync(); // 所有步骤成功后才正式提交。
            TempData["Message"] = "Purchase successful. The item is reserved for you. Contact the seller to arrange payment and pickup.";
            return RedirectToAction(nameof(Index)); // 到交易页面查看这笔 Accepted 购买。
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            // 同时购买时 SQL Server 偶尔会选择一个请求重试；不能把失败显示成购买成功。
            TempData["Message"] = "Another buyer is purchasing this item. Please return to the item page and try again.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            // SaveChanges 中发生死锁时 EF Core 会把 SqlException 包在 DbUpdateException 里面。
            TempData["Message"] = "Another buyer is purchasing this item. Please return to the item page and try again.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }
    }

    [HttpPost] // 接受请求会改变商品和请求状态。
    [ValidateAntiForgeryToken] // 检查本站表单令牌。
    public async Task<IActionResult> Accept(int id)
    {
        var userId = _userManager.GetUserId(User); // 读取当前卖家身份。
        // 事务像“一个整体步骤”：检查、预订、拒绝其他买家、建立通知要一起成功。
        // Serializable 会让数据库保护检查过的数据，避免两个请求同时都认为同一商品还能卖。
        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var request = await _db.TradeRequests.Include(x => x.Listing) // 读取请求及它的商品。
                .FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                return NotFound(); // using 在离开方法时自动释放未提交事务。
            }
            if (request.SellerId != userId)
            {
                return Forbid(); // 只有此请求的卖家能接受。
            }
            if (request.Status != "Pending")
            {
                return RedirectToAction(nameof(Index)); // 已经处理过的请求不重复处理或重复发通知。
            }

            var listing = request.Listing; // 简单变量名便于后面阅读。
            if (listing == null || listing.IsHidden || !listing.IsAvailable || listing.Status != "Available")
            {
                TempData["Message"] = "This item is no longer available for this request."; // 避免接受隐藏或售完商品的旧请求。
                return RedirectToAction(nameof(Index)); // 让用户看到交易列表当前状态。
            }

            var alreadyTaken = await _db.TradeRequests.AnyAsync(x =>
                x.ListingId == request.ListingId && x.Id != id &&
                (x.Status == "Accepted" || x.Status == "Completed")); // 再查是否已经答应过别的买家。
            if (alreadyTaken)
            {
                TempData["Message"] = "Another trade already reserves or completes this item."; // 即使商品状态被旧代码改错也不能重复出售。
                return RedirectToAction(nameof(Index)); // 不修改任何状态。
            }

            request.Status = "Accepted"; // 当前请求已接受。
            request.UpdatedAt = DateTime.UtcNow; // 记录处理时间，统一存 UTC。
            listing.Status = "Reserved"; // 商品已被预订。
            listing.IsAvailable = false; // 从可交易商品列表里移除。
            listing.UpdatedAt = DateTime.UtcNow; // 记录商品状态修改时间。
            AddTradeNotification(request.BuyerId, "Your trade request was accepted. The item is reserved for you."); // 通知获选买家。

            var otherRequests = await _db.TradeRequests.Where(x =>
                x.ListingId == request.ListingId && x.Id != id && x.Status == "Pending").ToListAsync(); // 查同商品的其他待处理请求。
            foreach (var other in otherRequests) // 一件商品不能同时答应给多个买家。
            {
                other.Status = "Rejected"; // 自动拒绝其余待处理请求。
                other.UpdatedAt = DateTime.UtcNow; // 记录处理时间。
                AddTradeNotification(other.BuyerId, "Your request was declined because this item was reserved for another buyer."); // 告知其他买家。
            }

            await _db.SaveChangesAsync(); // 把所有对象变化写进当前事务。
            await transaction.CommitAsync(); // 到这里才确认整体操作成功并释放数据库锁。
            TempData["Message"] = "Request accepted. The item is now reserved."; // 显示结果。
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {
            // 极少数同时接受操作会互相等待；SQL Server 会撤回其中一个，不能假装成功。
            TempData["Message"] = "Another trade was updated at the same time. Please refresh and try again.";
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {
            // 如果死锁发生在 SaveChanges，EF Core 会包装错误；同样提示刷新重试。
            TempData["Message"] = "Another trade was updated at the same time. Please refresh and try again.";
        }

        return RedirectToAction(nameof(Index)); // 成功或需要重试，都回交易列表。
    }

    [HttpPost] // 拒绝请求属于修改操作。
    [ValidateAntiForgeryToken] // 检查防伪令牌。
    public async Task<IActionResult> Reject(int id)
    {
        var userId = _userManager.GetUserId(User); // 当前操作人。
        var request = await _db.TradeRequests.FindAsync(id); // 查请求。
        if (request == null)
        {
            return NotFound(); // 无此请求。
        }
        if (request.SellerId != userId)
        {
            return Forbid(); // 只有卖家可以拒绝。
        }
        if (request.Status == "Pending") // 不重复处理已完成或已拒绝的请求。
        {
            request.Status = "Rejected"; // 设置拒绝状态。
            request.UpdatedAt = DateTime.UtcNow; // 记录修改时间。
            AddTradeNotification(request.BuyerId, "Your trade request was declined."); // 通知买家。
            await _db.SaveChangesAsync(); // 状态和通知一起保存。
        }
        return RedirectToAction(nameof(Index)); // 回交易列表。
    }

    [HttpPost] // 取消请求属于修改操作。
    [ValidateAntiForgeryToken] // 检查防伪令牌。
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User); // 当前操作人。
        var request = await _db.TradeRequests.FindAsync(id); // 查请求。
        if (request == null)
        {
            return NotFound(); // 无此请求。
        }
        if (request.BuyerId != userId)
        {
            return Forbid(); // 只有发起请求的买家可以取消。
        }
        if (request.Status == "Pending") // 现有规则只允许取消尚未被卖家处理的请求。
        {
            request.Status = "Cancelled"; // 记录买家已取消。
            request.UpdatedAt = DateTime.UtcNow; // 记录修改时间。
            AddTradeNotification(request.SellerId, "A buyer cancelled their pending trade request."); // 告知卖家。
            await _db.SaveChangesAsync(); // 请求状态和通知一起保存。
        }
        return RedirectToAction(nameof(Index)); // 回交易列表。
    }

    [HttpPost] // 完成交易属于修改操作。
    [ValidateAntiForgeryToken] // 检查防伪令牌。
    public async Task<IActionResult> Complete(int id)
    {
        var userId = _userManager.GetUserId(User); // 当前操作人。
        var request = await _db.TradeRequests.Include(x => x.Listing) // 同时查询要更新的商品。
            .FirstOrDefaultAsync(x => x.Id == id);
        if (request == null)
        {
            return NotFound(); // 请求不存在。
        }
        if (request.SellerId != userId)
        {
            return Forbid(); // 只有卖家能确认交易完成。
        }
        if (request.Status != "Accepted")
        {
            return RedirectToAction(nameof(Index)); // 必须先接受，再完成。
        }

        request.Status = "Completed"; // 请求进入完成状态。
        request.UpdatedAt = DateTime.UtcNow; // 保存完成时间。
        if (request.Listing != null) // 有关联商品时同步它的状态。
        {
            // 展开原来的 switch 表达式，用普通 if/else 更容易逐步阅读。
            if (request.RequestType == "Swap")
            {
                request.Listing.Status = "Swapped"; // 交换已完成。
            }
            else if (request.RequestType == "Giveaway")
            {
                request.Listing.Status = "Given away"; // 赠送已完成。
            }
            else
            {
                request.Listing.Status = "Sold"; // 普通购买已完成。
            }
            if (request.Listing.IsHidden)
            {
                request.Listing.Status = "Unavailable"; // 完成旧交易也不能撤销管理员的隐藏决定。
            }
            request.Listing.IsAvailable = false; // 无论哪种交易，完成后都不能再出售。
            request.Listing.UpdatedAt = DateTime.UtcNow; // 更新商品时间。
        }

        AddTradeNotification(request.BuyerId, "Your trade was marked as completed by the seller."); // 告知买家交易完成。
        await _db.SaveChangesAsync(); // 交易、商品、通知同一次保存。
        TempData["Message"] = "Trade completed."; // 显示完成提示。
        return RedirectToAction(nameof(Index)); // 回交易列表。
    }

    // 多个操作都会通知交易另一方，把相同的七八行放在这里，避免复制后漏改。
    // 这个方法只“准备新增”，不会单独 SaveChanges，保证通知与对应交易操作一起提交。
    private void AddTradeNotification(string recipientId, string content)
    {
        if (recipientId == _userManager.GetUserId(User)) // 不给正在操作的本人发重复提醒。
        {
            return;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = recipientId, // 收到通知的人。
            Type = "Trade", // 交易类通知。
            Content = content, // 简短说明，不重复保存交易备注里的私人信息。
            RelatedUrl = "/TradeRequests" // 点击通知后回自己的交易列表。
        });
    }
}
