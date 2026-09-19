using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
//引入EFcore的框架
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models;

namespace WebApplication1.Data
{
    //ApplicationDbContext 继承 IdentityDbContext<AppUser>
    public class ApplicationDbContext : IdentityDbContext<AppUser>
    {
        //options为数据库连接和 EF Core 的各种配置
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            //把 options 传给父类 IdentityDbContext
            : base(options)
        {
        }

        // DbSet 是在 C# 中访问一张表的入口，并不是表中的实际数据。
        // => Set<Category>() 等价于只有 get 的属性，每次取出对应实体集合。
        public DbSet<Category> Categories => Set<Category>();
        // 商品表：标题、价格、卖家、状态等。
        public DbSet<Listing> Listings => Set<Listing>();
        // 图片地址表，不存图片文件本身。
        public DbSet<ListingPhoto> ListingPhotos => Set<ListingPhoto>();
        // 用户收藏关系表。
        public DbSet<Favourite> Favourites => Set<Favourite>();
        // 买卖双方围绕商品建立的会话。
        public DbSet<Conversation> Conversations => Set<Conversation>();
        // 会话里的每条消息。
        public DbSet<Message> Messages => Set<Message>();
        // 买卖、交换、赠送的请求状态。
        public DbSet<TradeRequest> TradeRequests => Set<TradeRequest>();
        // 新增：每位用户自己的站内通知。
        public DbSet<Notification> Notifications => Set<Notification>();
        // 新增：举报及管理员处理记录。
        public DbSet<Report> Reports => Set<Report>();

        // 描述表结构和关联规则；修改后需要生成 Migration 才能更新真实数据库。
        protected override void OnModelCreating(ModelBuilder builder)
        {
            // 保留父类配置的 AspNetUsers/AspNetRoles 等 Identity 表。
            base.OnModelCreating(builder);

            // 配置用户资料的长度和唯一学号。
            builder.Entity<AppUser>(entity =>
            {
                entity.Property(x => x.StudentId).HasMaxLength(30);
                entity.Property(x => x.Nickname).HasMaxLength(100);
                entity.Property(x => x.PickupCampus).HasMaxLength(150);
                entity.HasIndex(x => x.StudentId).IsUnique();
            });

            // 分类名称唯一，并让迁移插入初始五个分类。
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

            // 商品与卖家、分类关联；外键保证这些编号真实存在。
            builder.Entity<Listing>(entity =>
            {
                // SQL 金额最多十位数字，其中两位为小数。
                entity.Property(x => x.Price).HasColumnType("decimal(10,2)");
                // 数据库本身也拒绝负价，不只依赖网页验证。
                entity.ToTable(table => table.HasCheckConstraint(
                    "CK_Listings_Price", "[Price] IS NULL OR [Price] >= 0"));
                // 索引加速按时间、分类和状态查找；不改变查询结果。
                entity.HasIndex(x => x.CreatedAt);
                entity.HasIndex(x => new { x.CategoryId, x.Status });
                // 一位卖家有多件商品；Restrict 避免删用户导致商品失去卖家。
                entity.HasOne(x => x.Seller)
                    .WithMany(x => x.Listings)
                    .HasForeignKey(x => x.SellerId)
                    .OnDelete(DeleteBehavior.Restrict);
                // 一个分类有多件商品；仍有商品时不能删除分类。
                entity.HasOne(x => x.Category)
                    .WithMany(x => x.Listings)
                    .HasForeignKey(x => x.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 删商品时级联删除照片记录；磁盘文件由 ListingsController 删除。
            builder.Entity<ListingPhoto>(entity =>
            {
                entity.HasOne(x => x.Listing)
                    .WithMany(x => x.Photos)
                    .HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            // 用户 ID + 商品 ID 组成主键，不能重复收藏同一商品。
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

            // 同一买家针对同一商品只能有一个会话。
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

            // 按会话和发送时间查询；删除会话会一起删除消息。
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

            // 交易关联商品、买家、卖家；用户外键采用限制删除以保留关联。
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

            // 通知以收件人和已读状态为常用查询条件。
            builder.Entity<Notification>(entity =>
            {
                // 加速导航栏未读数量和个人通知列表。
                entity.HasIndex(x => new { x.UserId, x.IsRead, x.CreatedAt });
                // 一位用户可以收到很多通知；删除用户前需明确处理这些记录。
                entity.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            // 举报记录和商品分开保存，商品被删除后仍可查看举报历史。
            builder.Entity<Report>(entity =>
            {
                // 保存时检查原状态，避免两位管理员同时处理同一条举报。
                entity.Property(x => x.Status).IsConcurrencyToken();
                // 加速管理员按待处理状态筛选。
                entity.HasIndex(x => new { x.Status, x.CreatedAt });
                // 两个并发请求也不能为同一人/商品插入重复待处理举报。
                entity.HasIndex(x => new { x.ReporterId, x.ListingId }).IsUnique()
                    .HasFilter("[Status] = N'Pending' AND [ListingId] IS NOT NULL");
                // 删除商品只清空关联编号；ListingTitle 快照保留被举报商品名。
                entity.HasOne(x => x.Listing).WithMany().HasForeignKey(x => x.ListingId)
                    .OnDelete(DeleteBehavior.SetNull);
                // 保留举报人与处理人的身份关联，防止无意级联删掉记录。
                entity.HasOne(x => x.Reporter).WithMany().HasForeignKey(x => x.ReporterId)
                    .OnDelete(DeleteBehavior.Restrict);
                entity.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
