namespace WebApplication1.Models;

// 会话代表“某个买家围绕某件商品与卖家的聊天”。
public class Conversation
{
    // 会话主键。
    public int Id { get; set; }
    // 外键：讨论的是哪件商品。
    public int ListingId { get; set; }
    // 外键：发起聊天的买家。
    public string BuyerId { get; set; } = string.Empty;
    // 外键：商品的卖家。
    public string SellerId { get; set; } = string.Empty;
    // 统一保存 UTC 创建时间。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 下面是导航属性，Include 后可读取完整关联对象。
    public Listing? Listing { get; set; }
    public AppUser? Buyer { get; set; }
    public AppUser? Seller { get; set; }
    // 一个会话包含多条消息。
    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
