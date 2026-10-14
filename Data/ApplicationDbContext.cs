using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data
{

    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {

        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)

            : base(options)
        {
        }



        public DbSet<Category> Categories => Set<Category>();

        public DbSet<Listing> Listings => Set<Listing>();

        public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();

        public DbSet<Favourite> Favourites => Set<Favourite>();

        public DbSet<Conversation> Conversations => Set<Conversation>();

        public DbSet<Message> Messages => Set<Message>();

        public DbSet<TradeRequest> TradeRequests => Set<TradeRequest>();

        public DbSet<Notification> Notifications => Set<Notification>();

        public DbSet<Report> Reports => Set<Report>();


        protected override void OnModelCreating(ModelBuilder builder)
        {

            base.OnModelCreating(builder);


            builder.Entity<AppUser>(entity =>
            {
                entity.Property(x => x.StudentId).HasMaxLength(30);
                entity.Property(x => x.Nickname).HasMaxLength(100);
                entity.Property(x => x.PickupCampus).HasMaxLength(150);
                entity.HasIndex(x => x.StudentId).IsUnique();
            });


            builder.Entity<Category>(entity =>
            {
                entity.HasIndex(x => x.Name).IsUnique();
                entity.HasData(
                    new Category { Id = 1, Name = "Textbooks", Description = "Course books and study materials" },
                    new Category { Id = 2, Name = "Electronics", Description = "Computers, accessories and devices" },
                    new Category { Id = 3, Name = "Furniture", Description = "Desks, chairs and room furniture" },
                    new Category { Id = 4, Name = "Clothing", Description = "Clothing and university apparel" },
                    new Category { Id = 5, Name = "Other", Description = "Other campus marketplace items" }
                );
            });


            builder.Entity<Listing>(entity =>
            {

                entity.Property(x => x.Price).HasColumnType("decimal(10,2)");

                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Listings_Price", "[Price] IS NULL OR [Price] >= 0"));

                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.CategoryId, x.Status });

                entity.HasOne(x => x.Seller)
                    .WithMany(x => x.Listings)
                    .HasForeignKey(x => x.SellerId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(x => x.Category)
                    .WithMany(x => x.Listings)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            builder.Entity<ListingPhoto>(entity =>
            {
                entity.HasOne(x => x.Listing)
                    .WithMany(x => x.Photos)
                    .HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });


            builder.Entity<Favourite>(entity =>
            {
                entity.HasKey(x => new { x.UserId, x.ListingId });
                entity.HasOne(x => x.User)
                    .WithMany(x => x.Favourites)
                    .HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Listing)
                    .WithMany(x => x.Favourites)
                    .HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });


            builder.Entity<Conversation>(entity =>
            {
                entity.HasIndex(x => new { x.ListingId, x.BuyerId }).IsUnique();
                entity.HasOne(x => x.Listing)
                    .WithMany(x => x.Conversations)
                    .HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Buyer)
                    .WithMany()
                    .HasForeignKey(x => x.BuyerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Seller)
                    .WithMany()
                    .HasForeignKey(x => x.SellerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            builder.Entity<Message>(entity =>
            {
                entity.HasIndex(x => new { x.ConversationId, x.SentAt });
                entity.HasOne(x => x.Conversation)
                    .WithMany(x => x.Messages)
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Sender)
                    .WithMany()
                    .HasForeignKey(x => x.SenderId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            builder.Entity<TradeRequest>(entity =>
            {
                entity.HasIndex(x => new { x.ListingId, x.Status });
                entity.HasOne(x => x.Listing)
                    .WithMany(x => x.TradeRequests)
                    .HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
                entity.HasOne(x => x.Buyer)
                    .WithMany()
                    .HasForeignKey(x => x.BuyerId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.Seller)
                    .WithMany()
                    .HasForeignKey(x => x.SellerId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            builder.Entity<Notification>(entity =>
            {

                entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });

                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });


            builder.Entity<Report>(entity =>
            {

                entity.Property(x => x.Status).IsConcurrencyToken();

                entity.HasIndex(x => new { x.Status, x.CreatedAt });

                entity.HasIndex(x => new { x.ReporterId, x.ListingId }).IsUnique()
                    .HasFilter("[Status] = N'Pending' AND [ListingId] IS NOT NULL");

                entity.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.SetNull);

                entity.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
