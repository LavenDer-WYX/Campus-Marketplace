// 引入表单输入的检查规则。
using System.ComponentModel.DataAnnotations;
// ValidateNever 表示某个只用来显示的属性不参加表单验证。
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

// 下面的类都放在项目的 Models 分组中。
namespace WebApplication1.Models;

// 管理员首页需要几种统计数字；把它们装进同一个对象交给网页。
// ViewModel 只给页面传数据，本身不会创建数据库表。
public class AdminDashboardViewModel
{
    // 平台上全部注册用户的数量。
    public int UserCount { get; set; }

    // 平台上全部商品的数量，包括已卖出和被隐藏的商品。
    public int ListingCount { get; set; }

    // 当前公开、可交易的商品数量。
    public int AvailableListingCount { get; set; }

    // 状态是 Completed 的交易请求数量。
    public int CompletedTradeCount { get; set; }

    // 等待管理员处理的举报数量。
    public int PendingReportCount { get; set; }
}

// 学生提交举报时只需要提交商品编号和原因，不能自己指定举报人或处理结果。
public class ReportCreateViewModel
{
    // 商品编号必须是大于零的整数；Controller 还会检查商品是否确实存在。
    [Range(1, int.MaxValue)]
    public int ListingId { get; set; }

    // 举报原因必填，最大长度和数据库中的 Report.Reason 保持一致。
    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;

    // 商品标题由服务器从数据库取得，只用来显示，不信任浏览器提交的标题。
    [ValidateNever]
    public string ListingTitle { get; set; } = string.Empty;
}

// 管理员处理举报时提交举报编号和说明；不能从网页提交新的举报状态。
public class AdminReportActionViewModel
{
    // 要处理哪一条举报。
    [Range(1, int.MaxValue)]
    public int Id { get; set; }

    // 管理员的处理说明，最多 1000 字符；隐藏商品时 Controller 会要求填写。
    [StringLength(1000)]
    public string? AdminNote { get; set; }

    // 这是服务器查询到的举报详情，只供页面展示，不参加输入验证。
    [ValidateNever]
    public Report? Report { get; set; }
}
