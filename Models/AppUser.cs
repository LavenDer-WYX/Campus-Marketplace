using Microsoft.AspNetCore.Identity;

using System.ComponentModel.DataAnnotations;

// IdentityUser 已包含 Id、Email、UserName、PasswordHash 等账号字段。
// DataAnnotations 为新增的学生资料添加验证规则。
namespace WebApplication1.Models
{
    // AppUser 在 IdentityUser 基础上增加本项目需要的学生资料。
    public class AppUser : IdentityUser
    {
        // 学号必填、最多 30 字；DbContext 还有唯一索引。
        [Required, StringLength(30)]
        public string StudentId { get; set; } = string.Empty;
        // 当前只代表邮箱域名检查通过，还没有真实邮件链接验证。
        public bool IsSchoolVerified { get; set; } = false;

        // 页面显示给其他用户的昵称。
        [Required, StringLength(100)]
        public string Nickname { get; set; } = string.Empty;

        // 用户常用的校园取货区域。
        [Required, StringLength(150)]
        public string PickupCampus { get; set; } = string.Empty;

        // 一个用户可以发布多件商品。
        public ICollection<Listing> Listings { get; set; } = new List<Listing>();
        // 一个用户可以收藏多件商品。
        public ICollection<Favourite> Favourites { get; set; } = new List<Favourite>();
    }
    // 角色名称集中成常量，避免代码里拼写不一致。
    public static class UserRoles
    {
        // 普通注册账号自动加入 Student。
        public const string Student = "Student";
        // Admin 只能由本地管理命令授予，注册表单不能选择。
        public const string Admin = "Admin";
    }
}
