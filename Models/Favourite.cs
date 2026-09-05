namespace WebApplication1.Models;

public class Favourite
{
    public string UserId { get; set; } = string.Empty;
    public int ListingId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AppUser? User { get; set; }
    public Listing? Listing { get; set; }
}
