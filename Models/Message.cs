using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 一条 Message 代表会话中的一段文字。
public class Message
{
    // 消息主键。
    public int Id { get; set; }
    // 外键：消息属于哪个会话。
    public int ConversationId { get; set; }
    // 外键：是谁发送的。
    public string SenderId { get; set; } = string.Empty;

    // 正文必填，最多 1000 字。
    [Required, StringLength(1000)]
    public string Content { get; set; } = string.Empty;

    // 消息发送时间。
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    // 接收者打开聊天页后设为 true。
    public bool IsRead { get; set; }

    // 导航属性，分别访问会话和发送者资料。
    public Conversation? Conversation { get; set; }
    public AppUser? Sender { get; set; }
}
