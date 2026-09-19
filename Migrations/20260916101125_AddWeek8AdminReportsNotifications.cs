using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WebApplication1.Migrations
{
    // 这是 EF Core 根据 Models 和 ApplicationDbContext 自动生成的 Week 8 数据库升级步骤。
    // Up 负责升级；Down 只在开发者主动回退 Migration 时使用。
    public partial class AddWeek8AdminReportsNotifications : Migration
    {
        // 将旧 Week 7 数据库升级到 Week 8，已有用户和商品不会被清空。
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 给 Listings 增加管理员隐藏标记；旧商品默认 false，所以继续正常显示。
            migrationBuilder.AddColumn<bool>(
                name: "IsHidden",
                table: "Listings",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // 创建站内通知表，UserId 外键指向 AspNetUsers。
            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Content = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    RelatedUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 创建举报表。ListingId 可空，使商品删除后仍保留举报历史。
            migrationBuilder.CreateTable(
                name: "Reports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReporterId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ListingId = table.Column<int>(type: "int", nullable: true),
                    ListingTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    AdminNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedById = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Reports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Reports_AspNetUsers_ReporterId",
                        column: x => x.ReporterId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_AspNetUsers_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Reports_Listings_ListingId",
                        column: x => x.ListingId,
                        principalTable: "Listings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            // 下面的索引用于加速未读通知、举报筛选和关联查询。
            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            // 筛选唯一索引防止同一用户对同一商品同时存在两条 Pending 举报。
            migrationBuilder.CreateIndex(
                name: "IX_Reports_ListingId",
                table: "Reports",
                column: "ListingId");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ReporterId_ListingId",
                table: "Reports",
                columns: new[] { "ReporterId", "ListingId" },
                unique: true,
                filter: "[Status] = N'Pending' AND [ListingId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_ResolvedById",
                table: "Reports",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_Reports_Status_CreatedAt",
                table: "Reports",
                columns: new[] { "Status", "CreatedAt" });
        }

        // 回退时按照依赖关系先删新表，再删 Listings.IsHidden 字段。
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "Reports");

            migrationBuilder.DropColumn(
                name: "IsHidden",
                table: "Listings");
        }
    }
}
