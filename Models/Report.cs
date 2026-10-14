
using System.ComponentModel.DataAnnotations;


namespace WebApplication1.Models;


public class Report
{

    public int Id { get; set; }


    [Required]
    public string ReporterId { get; set; } = string.Empty;


    public int? ListingId { get; set; }


    [Required, StringLength(200)]
    public string ListingTitle { get; set; } = string.Empty;


    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;


    [Required, StringLength(30), RegularExpression("Pending|Dismissed|ActionTaken")]
    public string Status { get; set; } = "Pending";


    [StringLength(1000)]
    public string? AdminNote { get; set; }


    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;


    public DateTime? ResolvedAt { get; set; }


    public string? ResolvedById { get; set; }


    public AppUser? Reporter { get; set; }


    public Listing? Listing { get; set; }


    public AppUser? ResolvedBy { get; set; }
}
