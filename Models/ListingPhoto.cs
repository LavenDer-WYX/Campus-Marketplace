using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

public class ListingPhoto
{
    public int Id { get; set; }

    [Required, StringLength(500)]
    public string ImageUrl { get; set; } = string.Empty;

    public int ListingId { get; set; }
    public Listing? Listing { get; set; }
}
