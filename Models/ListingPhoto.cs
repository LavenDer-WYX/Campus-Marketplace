using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 数据库只保存图片 URL，真实文件在 wwwroot/uploads/listings。
public class ListingPhoto
{
    // 照片记录主键。
    public int Id { get; set; }

    // 浏览器访问的相对地址，最长 500 字。
    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    // 外键：照片属于哪件商品。
    public int ListingId { get; set; }
    // 导航属性，Include 后能访问完整商品。
    public Listing? Listing { get; set; }
}
