using Microsoft.EntityFrameworkCore;
using CampusMarketplace1.Models;

namespace CampusMarketplace1.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<Listing> Listings { get; set; }
        public DbSet<ListingPhoto> ListingPhotos { get; set; }
    }
}