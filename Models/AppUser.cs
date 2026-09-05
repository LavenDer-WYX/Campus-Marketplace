using Microsoft.AspNetCore.Identity;

using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models
{
    public class AppUser : IdentityUser
    {
        [Required, StringLength(30)]
        public string StudentId { get; set; } = string.Empty;
        public bool IsSchoolVerified { get; set; } = false;

        [Required, StringLength(100)]
        public string Nickname { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string PickupCampus { get; set; } = string.Empty;

        public ICollection<Listing> Listings { get; set; } = new List<Listing>();
        public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();
    }
    public static class UserRoles
    {
        public const string Student = "Student";
        public const string Admin = "Admin";
    }
}
