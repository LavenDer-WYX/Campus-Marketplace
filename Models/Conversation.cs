namespace WebApplication1.Models;


public class Conversation
{

    public int Id { get; set; }

    public int ListingId { get; set; }

    public string BuyerId { get; set; } = string.Empty;

    public string SellerId { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public Listing? Listing { get; set; }
    public AppUser? Buyer { get; set; }
    public AppUser? Seller { get; set; }

    public ICollection<Message> Messages { get; set; } = new List<Message>();
}
