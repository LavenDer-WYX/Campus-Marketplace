namespace WebApplication1.Models;

public class MessagePageViewModel
{
    public Conversation Conversation { get; set; } = new();
    public IReadOnlyList<Message> Messages { get; set; } = Array.Empty<Message>();
}
