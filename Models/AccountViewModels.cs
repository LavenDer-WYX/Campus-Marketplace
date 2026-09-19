// DataAnnotations 给表单字段添加必填、邮箱、长度等验证规则。
using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;

// 注册页面专用对象；它只接收表单，不会单独创建数据库表。
public class RegisterViewModel
{
    // 必填且必须符合普通邮箱格式；Controller 还会检查学校域名。
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // 学号必填且最多 30 字；数据库还有唯一索引防止重复。
    [Required, StringLength(30)]
    public string StudentId { get; set; } = string.Empty;

    // 页面显示昵称，最多 100 字。
    [Required, StringLength(100)]
    public string Nickname { get; set; } = string.Empty;

    // 常用校区或取货区域，最多 150 字。
    [Required, StringLength(150)]
    public string PickupCampus { get; set; } = string.Empty;

    // 密码输入框隐藏字符，至少八位；复杂度由 Identity 再检查。
    [Required, DataType(DataType.Password), MinLength(8)]
    public string Password { get; set; } = string.Empty;

    // 确认密码必须与 Password 相同，但本字段不会保存进数据库。
    [Required, DataType(DataType.Password), Compare(nameof(Password))]
    public string ConfirmPassword { get; set; } = string.Empty;
}

// 登录页面只需要邮箱、密码和是否记住登录。
public class LoginViewModel
{
    // 登录名使用大学邮箱。
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // 原始密码只用于验证；Identity 数据库保存的是 PasswordHash。
    [Required, DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    // true 会建立有效期更长的登录 Cookie。
    public bool RememberMe { get; set; }
}
