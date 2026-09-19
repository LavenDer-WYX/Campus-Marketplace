using Microsoft.AspNetCore.Authorization; // 提供登录权限检查。
using Microsoft.AspNetCore.Identity; // 提供 UserManager。
using Microsoft.AspNetCore.Mvc; // 提供 Controller、View 和跳转。
using Microsoft.EntityFrameworkCore; // 提供数据库异步查询。
using WebApplication1.Data; // 引入 ApplicationDbContext。
using WebApplication1.Models; // 引入聊天、消息、通知实体。

namespace WebApplication1.Controllers; // 控制器命名空间。

[Authorize] // 只有登录用户能使用聊天。
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db; // 数据库入口。
    private readonly UserManager<AppUser> _userManager; // 当前账号身份入口。

    public MessagesController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db; // 保存框架自动传来的数据库服务。
        _userManager = userManager; // 保存账号服务。
    }

    // 会话列表：Conversation 是一组聊天，Message 才是其中的一句话。
    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User); // 从登录 Cookie 读取用户 ID。
        var conversations = await _db.Conversations.AsNoTracking() // 列表只读，不修改实体。
            .Include(x => x.Listing) // 查询聊天涉及的商品。
            .Include(x => x.Buyer) // 查询买家昵称。
            .Include(x => x.Seller) // 查询卖家昵称。
            .Include(x => x.Messages) // 查询消息，以便列表显示最后一条消息和未读数。
            .Where(x => x.BuyerId == userId || x.SellerId == userId) // 自己必须是参与者。
            // Max 找最后一条消息的时间；空会话没有消息，就用会话建立时间。
            .OrderByDescending(x => x.Messages.Max(m => (DateTime?)m.SentAt) ?? x.CreatedAt)
            .ToListAsync(); // 执行数据库查询。

        return View(conversations); // 返回 Views/Messages/Index.cshtml。
    }

    [HttpPost] // 联系卖家可能建立会话，所以使用 POST。
    [ValidateAntiForgeryToken] // 验证本站表单提交。
    public async Task<IActionResult> Start(int listingId)
    {
        var userId = _userManager.GetUserId(User); // 读取当前登录人。
        if (userId == null)
        {
            return Challenge(); // 身份无效时要求登录。
        }

        var listing = await _db.Listings.FindAsync(listingId); // 查询商品和卖家 ID。
        if (listing == null || listing.IsHidden)
        {
            return NotFound(); // 被隐藏或删除的商品不能建立新会话，旧会话仍可保留查看。
        }

        if (listing.SellerId == userId) // 不能给自己发起买卖聊天。
        {
            TempData["Message"] = "You cannot message yourself about your own item."; // 下个页面显示原因。
            return RedirectToAction("Details", "Listings", new { id = listingId }); // 返回商品页。
        }

        var conversation = await _db.Conversations // 先查此用户对此商品是否已有会话。
            .FirstOrDefaultAsync(x => x.ListingId == listingId && x.BuyerId == userId);

        if (conversation == null) // 已有会话就复用，没有才建立。
        {
            conversation = new Conversation
            {
                ListingId = listingId, // 聊哪件商品。
                BuyerId = userId, // 谁点击“联系卖家”。
                SellerId = listing.SellerId // 卖家来自数据库，不能由客户端指定。
            };
            _db.Conversations.Add(conversation); // 标记会话等待插入。
            await _db.SaveChangesAsync(); // 保存后数据库生成 conversation.Id。
        }

        return RedirectToAction(nameof(Chat), new { id = conversation.Id }); // 打开对应聊天页。
    }

    // GET /Messages/Chat/会话编号：显示聊天内容，并把对方消息标为已读。
    public async Task<IActionResult> Chat(int id)
    {
        var userId = _userManager.GetUserId(User); // 当前查看者 ID。
        var conversation = await _db.Conversations
            .Include(x => x.Listing) // 页面显示对应商品。
            .Include(x => x.Buyer) // 页面显示买家。
            .Include(x => x.Seller) // 页面显示卖家。
            .FirstOrDefaultAsync(x => x.Id == id); // 查询指定会话。

        if (conversation == null)
        {
            return NotFound(); // 会话不存在返回 404。
        }
        if (conversation.BuyerId != userId && conversation.SellerId != userId)
        {
            return Forbid(); // 即使知道 ID，其他用户也不能偷看。
        }

        var messages = await _db.Messages // 此处不用 AsNoTracking，因为下面要修改已读状态。
            .Include(x => x.Sender) // 页面需要发送者昵称。
            .Where(x => x.ConversationId == id) // 只查当前会话。
            .OrderBy(x => x.SentAt) // 从最早消息开始显示。
            .ToListAsync(); // 执行查询。

        foreach (var message in messages) // 依次检查所有消息，展开原先较短的 LINQ 写法方便学习。
        {
            if (message.SenderId != userId && !message.IsRead) // 只改对方发来的未读消息。
            {
                message.IsRead = true; // 修改对象，EF 会记住变化。
            }
        }
        await _db.SaveChangesAsync(); // 把已读变化写回数据库。

        var model = new MessagePageViewModel
        {
            Conversation = conversation, // 会话信息。
            Messages = messages // 本次聊天的全部消息。
        };
        return View(model); // 一个 ViewModel 把两部分数据交给页面。
    }

    [HttpPost] // 发送消息会写数据库。
    [ValidateAntiForgeryToken] // 验证本站请求令牌。
    public async Task<IActionResult> Send(int conversationId, string content)
    {
        var userId = _userManager.GetUserId(User); // 当前发送者由身份确定。
        if (userId == null)
        {
            return Challenge(); // 登录失效就要求重登。
        }

        var conversation = await _db.Conversations.FindAsync(conversationId); // 查会话是否存在。
        if (conversation == null)
        {
            return NotFound(); // 不存在就不能发送。
        }
        if (conversation.BuyerId != userId && conversation.SellerId != userId)
        {
            return Forbid(); // 发送者必须是这个会话里的买家或卖家。
        }

        content = content?.Trim() ?? string.Empty; // 去掉两头空格；没有传内容时按空字符串处理。
        if (content.Length == 0 || content.Length > 1000) // 服务端再次验证，不能只相信网页验证。
        {
            TempData["Message"] = "Message must be between 1 and 1000 characters."; // 设置错误提示。
            return RedirectToAction(nameof(Chat), new { id = conversationId }); // 回聊天页让用户修改。
        }

        _db.Messages.Add(new Message
        {
            ConversationId = conversationId, // 消息属于哪个会话。
            SenderId = userId, // 消息是谁发送的。
            Content = content // 保存正文；Razor 默认 HTML 编码后显示。
        });

        var recipientId = conversation.BuyerId; // 默认通知买家。
        if (userId == conversation.BuyerId)
        {
            recipientId = conversation.SellerId; // 买家发消息时，收件人应是卖家。
        }
        if (recipientId != userId) // 防止错误数据导致给自己发通知。
        {
            _db.Notifications.Add(new Notification
            {
                UserId = recipientId, // 通知属于对方，不属于发送者。
                Type = "Message", // 分类让通知页面知道这是聊天提醒。
                Content = "You have a new marketplace message.", // 不复制私聊正文，减少无必要的信息展示。
                RelatedUrl = "/Messages/Chat/" + conversationId // 点击后回对应会话；Chat 仍会再次检查权限。
            });
        }

        // 同一次 SaveChanges 提交消息和通知：EF Core 默认事务保证一起成功或一起失败。
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Chat), new { id = conversationId }); // 重新读取聊天页，显示刚发出的消息。
    }
}
