using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;


public class Message
{

    public int Id { get; set; }

    public int ConversationId { get; set; }

    public string SenderId { get; set; } = string.Empty;


    [Required, StringLength(1000)]
    public string Content { get; set; } = string.Empty;


    public DateTime SentAt { get; set; } = DateTime.UtcNow;

    public bool IsRead { get; set; }


    public Conversation? Conversation { get; set; }
    public AppUser? Sender { get; set; }
}
