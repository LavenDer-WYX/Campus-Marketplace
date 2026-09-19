// 引入必填、长度、范围等验证规则。
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 一个对象代表一件商品；EF Core 将它保存到 Listings 表。
public class Listing
{
    // 商品主键，保存时由数据库自动分配。
    public int Id { get; set; }

    // 标题必填，最多 200 字；get 可读取，set 可修改。
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    // 描述必填且最多 2000 字；初始值为空字符串。
    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;

    // decimal 保存金额；? 表示交换、赠送商品可以没有价格。
    [Range(0, 999999.99)]
    public decimal? Price { get; set; }

    // 商品只能是出售、交换或赠送，默认出售。
    [Required, StringLength(30), RegularExpression("Sale|Swap|Giveaway")]
    public string ListingType { get; set; } = "Sale";

    // 成色只能从这四项里选择。
    [Required, StringLength(50), RegularExpression("Like new|Good|Fair|Poor")]
    public string Condition { get; set; } = string.Empty;

    // 约定取货地点，最多 200 字。
    [Required, StringLength(200)]
    public string PickupLocation { get; set; } = string.Empty;

    // 业务状态：可交易、预订、出售、交换、赠送、不可用。
    [Required, StringLength(50), RegularExpression("Available|Reserved|Sold|Swapped|Given away|Unavailable")]
    public string Status { get; set; } = "Available";

    // 浏览列表同时检查 IsAvailable 和 Status。
    public bool IsAvailable { get; set; } = true;
    // 管理员下架标记；卖家表单不能修改，防止自行恢复违规商品。
    public bool IsHidden { get; set; }
    // UTC 是统一时间，显示到页面时再转成本地时间。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // 修改商品时由 Controller 更新时间。
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 外键：关联 AspNetUsers.Id；服务器从登录身份获得卖家 ID。
    [Required]
    public string SellerId { get; set; } = string.Empty;

    // 外键：关联 Categories.Id；分类编号必须大于零。
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }
    // 导航属性：Include(Seller) 后可读取昵称等卖家资料。
    public AppUser? Seller { get; set; }
    // 导航属性：Include(Category) 后可读取分类名称。
    public Category? Category { get; set; }
    // 一件商品有多张照片；真实文件在 wwwroot，记录里保存 URL。
    public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();
    // 一件商品可被多位用户收藏。
    public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();
    // 不同买家对同一商品可分别发起聊天。
    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();
    // 保存与商品有关的购买、交换或领取请求。
    public ICollection<TradeRequest> TradeRequests { get; set; } = new List<TradeRequest>();
}
