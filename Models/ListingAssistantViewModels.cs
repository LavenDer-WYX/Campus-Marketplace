// DataAnnotations 提供 Required、StringLength 等表单检查规则。
using System.ComponentModel.DataAnnotations;

// 这个文件只放商品助手的输入和输出对象，不会新增数据库表。
namespace WebApplication1.Models;

// 页面点击 Generate draft 时，MVC 会把表单数据装进这个对象。
public class GenerateDescriptionViewModel
{
    // 商品名称必填，长度与 Listing.Title 一致。
    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;

    // 只接收分类编号，真正的分类名称由服务器从数据库读取。
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    // 只允许网站已有的四种新旧程度，防止随意传入别的值。
    [Required, RegularExpression("^(Like new|Good|Fair|Poor)$")]
    public string Condition { get; set; } = string.Empty;

    // 使用时间可不填；例如 Used for one year，最多 80 个字符。
    [StringLength(80)]
    public string? Age { get; set; }

    // 配件信息可不填，最多 300 个字符；不需要用户的联系方式。
    [StringLength(300)]
    public string? Accessories { get; set; }
}

// 价格参考只需要分类与新旧程度，不需要向外部 AI 发送信息。
public class PriceSuggestionViewModel
{
    // 分类编号必须是正整数。
    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    // 仍然只允许已有的新旧程度。
    [Required, RegularExpression("^(Like new|Good|Fair|Poor)$")]
    public string Condition { get; set; } = string.Empty;
}

// 服务把 AI 调用结果放在这个简单对象中，再交给 Controller 返回 JSON。
public class DescriptionResult
{
    // HTTP 状态码：200 成功；503 未配置；502 服务错误；504 超时。
    public int StatusCode { get; set; }

    // 只有成功取得可用草稿时才是 true。
    public bool Success { get; set; }

    // 页面显示给用户的说明，不包含 API Key 或服务商的原始错误。
    public string Message { get; set; } = string.Empty;

    // 草稿先供用户检查，不会自动保存成商品。
    public string? Description { get; set; }
}

// 返回页面的价格参考；它来自数据库统计，不是 AI 生成的估价。
public class PriceSuggestionResult
{
    // 没有任何符合条件的商品时是 false。
    public bool HasData { get; set; }

    // 本次实际参与计算的商品数量，最多 100 条。
    public int SampleCount { get; set; }

    // 没有相同新旧程度的商品时，才退回到整个分类。
    public bool UsedCategoryOnly { get; set; }

    // 参考金额是样本的平均挂牌价；没有数据时保持 null。
    public decimal? SuggestedPrice { get; set; }

    // 展示样本最低和最高挂牌价，帮助用户知道价格分布很宽还是很窄。
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }

    // 当前网站的价格以新西兰元展示。
    public string Currency { get; set; } = "NZD";

    // 解释样本来源、回退规则，以及挂牌价不等于真实成交价。
    public string Message { get; set; } = string.Empty;
}
