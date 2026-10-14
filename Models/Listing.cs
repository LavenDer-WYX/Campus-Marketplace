
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;


public class Listing
{

    public int Id { get; set; }


    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;


    [Required, StringLength(2000)]
    public string Description { get; set; } = string.Empty;


    [Range(0, 999999.99)]
    public decimal? Price { get; set; }


    [Required, StringLength(30), RegularExpression("Sale|Swap|Giveaway")]
    public string ListingType { get; set; } = "Sale";


    [Required, StringLength(50), RegularExpression("Like new|Good|Fair|Poor")]
    public string Condition { get; set; } = string.Empty;


    [Required, StringLength(200)]
    public string PickupLocation { get; set; } = string.Empty;


    [Required, StringLength(50), RegularExpression("Available|Reserved|Sold|Swapped|Given away|Unavailable")]
    public string Status { get; set; } = "Available";


    public bool IsAvailable { get; set; } = true;

    public bool IsHidden { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;


    [Required]
    public string SellerId { get; set; } = string.Empty;


    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public AppUser? Seller { get; set; }

    public Category? Category { get; set; }

    public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();

    public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();

    public ICollection<Conversation> Conversations { get; set; } = new List<Conversation>();

    public ICollection<TradeRequest> TradeRequests { get; set; } = new List<TradeRequest>();
}
