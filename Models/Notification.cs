// 引入字段验证规则。
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 每个对象代表发给某一个用户的一条站内通知。
public class Notification
{
    // 通知主键，由数据库自动生成。
    public int Id { get; set; }
    // 收件人 ID；只能由后端从消息/交易参与者中选取。
    [Required]
    public string UserId { get; set; } = string.Empty;
    // 类型区分消息、交易和举报处理通知。
    [Required, StringLength(30)]
    public string Type { get; set; } = string.Empty;
    // 不复制完整私信，避免在通知提示里泄露聊天内容。
    [Required, StringLength(1000)]
    public string Content { get; set; } = string.Empty;
    // 本站地址由 Controller 生成，不接收任意外部网址。
    [StringLength(500)]
    public string? RelatedUrl { get; set; }
    // 默认 false，即未读。
    public bool IsRead { get; set; }
    // 自动记录创建时间。
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    // 导航属性，需要时可查询收件人资料。
    public AppUser? User { get; set; }
}
