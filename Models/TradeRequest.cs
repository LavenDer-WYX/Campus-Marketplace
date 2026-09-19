using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 一条交易请求记录买家对商品的购买、交换或领取申请。
public class TradeRequest
{
    // 请求主键。
    public int Id { get; set; }
    // 商品、买家、卖家的外键。
    public int ListingId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;

    // Buy、Swap 或 Giveaway，最多 20 字。
    [Required, StringLength(20)]
    public string RequestType { get; set; } = "Buy";

    // 买家给卖家的可选备注。
    [StringLength(500)]
    public string? Note { get; set; }

    // Pending、Accepted、Rejected、Cancelled 或 Completed。
    // 普通请求从 Pending 开始；Buy now 会直接从 Accepted 开始。
    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";
    // 记录创建时间和最后一次状态更新时间。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性，Include 后可访问关联对象。
    public Listing? Listing { get; set; }
    public AppUser? Buyer { get; set; }
    public AppUser? Seller { get; set; }
}
