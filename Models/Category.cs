using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 商品分类实体，对应 Categories 表。
public class Category
{
    // 分类主键；五个初始分类使用固定编号 1 到 5。
    public int Id { get; set; }

    // 分类名称必填，DbContext 还要求唯一。
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    // 描述可空，所以 string 后面有问号。
    [StringLength(500)]
    public string? Description { get; set; }

    // 一个分类下面可以有多件商品。
    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
