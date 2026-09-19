namespace WebApplication1.Models;

// 收藏是用户和商品之间的关系；联合主键是 UserId + ListingId。
public class Favourite
{
    // 收藏者的 AspNetUsers.Id。
    public string UserId { get; set; } = string.Empty;
    // 被收藏商品的 Listings.Id。
    public int ListingId { get; set; }
    // 用于按最新收藏排序。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性，查询 Include 后可读取用户和商品。
    public AppUser? User { get; set; }
    public Listing? Listing { get; set; }
}
