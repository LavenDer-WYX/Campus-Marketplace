using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class TradeRequest
{
    public int Id { get; set; }
    public int ListingId { get; set; }
    public string BuyerId { get; set; } = string.Empty;
    public string SellerId { get; set; } = string.Empty;

    [Required, StringLength(20)]
    public string RequestType { get; set; } = "Buy";

    [StringLength(500)]
    public string? Note { get; set; }

    [Required, StringLength(20)]
    public string Status { get; set; } = "Pending";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Listing? Listing { get; set; }
    public AppUser? Buyer { get; set; }
    public AppUser? Seller { get; set; }
}
