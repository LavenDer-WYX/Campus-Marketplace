using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusMarketplace1.Models
{
    public class Listing
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        [Required]
        [StringLength(50)]
        public string Condition { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string PickupLocation { get; set; } = string.Empty;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } = "Available";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int UserId { get; set; }

        public int CategoryId { get; set; }

        public User User { get; set; } = null!;

        public Category Category { get; set; } = null!;

        public ICollection<ListingPhoto> Photos { get; set; } = new List<ListingPhoto>();
    }
}