// 引入 Required、StringLength 等检查数据的规则。
using System.ComponentModel.DataAnnotations;

// 这个文件里的类属于项目的 Models 分组。
namespace WebApplication1.Models;

// 一条 Report 代表一个学生对一个商品提交的一次举报。
public class Report
{
    // 数据库自动生成的举报编号，用来区分每条举报。
    public int Id { get; set; }

    // 举报人必须是一个已经注册的用户；这里保存用户的 Id。
    [Required]
    public string ReporterId { get; set; } = string.Empty;

    // 商品可能后来被卖家删除，所以商品编号允许变成 null，举报历史仍然保留。
    public int? ListingId { get; set; }

    // 保存提交举报时的商品标题，即使商品改名或被删除也能说明举报的是哪个商品。
    [Required, StringLength(200)]
    public string ListingTitle { get; set; } = string.Empty;

    // 学生填写的举报原因，必须填写并且不能超过 1000 个字符。
    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    // Pending 是待处理；Dismissed 是不采取措施；ActionTaken 是已隐藏商品。
    [Required, StringLength(30), RegularExpression("Pending|Dismissed|ActionTaken")]
    public string Status { get; set; } = "Pending";

    // 管理员记录处理说明；没有处理时可以为空。
    [StringLength(1000)]
    public string? AdminNote { get; set; }

    // 使用 UTC 保存提交时间，避免服务器所在时区改变后时间含义发生变化。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 尚未处理时没有处理时间，所以这里使用 DateTime?。
    public DateTime? ResolvedAt { get; set; }

    // 记录处理这条举报的管理员 Id；待处理的举报还没有这个值。
    public string? ResolvedById { get; set; }

    // 导航属性：让 EF Core 可以通过 ReporterId 读取举报人的资料。
    public AppUser? Reporter { get; set; }

    // 导航属性：让 EF Core 可以通过 ListingId 读取商品；商品删除后这里为空。
    public Listing? Listing { get; set; }

    // 导航属性：让 EF Core 可以读取处理举报的管理员资料。
    public AppUser? ResolvedBy { get; set; }
}
