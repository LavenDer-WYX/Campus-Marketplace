namespace WebApplication1.Models;

// 聊天页同时需要会话资料和消息列表。
public class MessagePageViewModel
{
    // new() 创建空会话，避免属性为 null。
    public Conversation Conversation { get; set; } = new();
    // 没有消息时使用安全的空数组。
    public IReadOnlyList<Message> Messages { get; set; } = Array.Empty<Message>();
}
