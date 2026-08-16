using System;
using System.ComponentModel.DataAnnotations;

namespace CampusMarketplace1.Models
{
	public class User
	{
		public int Id { get; set; }

		[Required]
		[StringLength(100)]
		public string UserName { get; set; } = string.Empty;

		[Required]
		[EmailAddress]
		[StringLength(200)]
		public string Email { get; set; } = string.Empty;

		[Required]
		public string PasswordHash { get; set; } = string.Empty;

		[Required]
		[StringLength(20)]
		public string Role { get; set; } = "Student";

		public bool IsVerified { get; set; } = false;

		public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Listing> Listings { get; set; } = new List<Listing>();
    }
}