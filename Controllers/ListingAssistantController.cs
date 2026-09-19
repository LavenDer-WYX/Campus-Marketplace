// Authorize 要求调用者已经登录。
using Microsoft.AspNetCore.Authorization;
// MVC 提供 Controller、Json 和各种 HTTP 特性。
using Microsoft.AspNetCore.Mvc;
// 只在 AI 生成接口启用 Program.cs 定义的限流规则。
using Microsoft.AspNetCore.RateLimiting;
// 提供数据库异步查询。
using Microsoft.EntityFrameworkCore;
// 引入项目自己的数据库、表单对象和助手服务。
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;

// 两个接口都要求登录，匿名访问不能使用商品助手。
[Authorize]
public class ListingAssistantController : Controller
{
    // Controller 只查询分类是否存在，把生成和价格计算交给简单服务。
    private readonly ApplicationDbContext _db;
    // 这个服务在 Program.cs 注册，ASP.NET Core 会自动创建。
    private readonly ListingAssistantService _assistant;

    // 构造函数接收所需对象，不需要在方法里手动 new DbContext。
    public ListingAssistantController(ApplicationDbContext db, ListingAssistantService assistant)
    {
        // 保存数据库入口。
        _db = db;
        // 保存助手服务。
        _assistant = assistant;
    }

    // POST /ListingAssistant/Generate：使用普通表单编码，不直接接收整件商品。
    [HttpPost]
    // 校验网页请求中的防伪令牌，避免其他网站偷偷消耗这个接口。
    [ValidateAntiForgeryToken]
    // 限流名称必须与 Program.cs 的注册名称一致。
    [EnableRateLimiting("listing-assistant")]
    public async Task<IActionResult> Generate(GenerateDescriptionViewModel input, CancellationToken cancellationToken)
    {
        // ModelState 包含 Required、StringLength 等规则的检查结果。
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Enter a title (up to 200 characters), a category and a valid condition. Age is limited to 80 characters and accessories to 300." });
        }
        // 浏览器只提供编号；从数据库读取真实分类名，避免相信浏览器传来的分类名称。
        string? categoryName = await _db.Categories.AsNoTracking()
            .Where(x => x.Id == input.CategoryId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);
        // 正数也不一定对应真实分类，因此还需要数据库检查。
        if (categoryName == null)
        {
            return BadRequest(new { success = false, message = "Please choose an existing category." });
        }
        // 服务负责外部请求、超时、响应检查；这里不处理 API Key。
        DescriptionResult result = await _assistant.GenerateDescriptionAsync(input, categoryName, cancellationToken);
        // 成功形状为 {success:true, description:"...", message:"..."}；失败 description 是 null。
        return StatusCode(result.StatusCode, new { success = result.Success, description = result.Description, message = result.Message });
    }

    // POST /ListingAssistant/Price：查询本地数据库，不使用外部 AI。
    [HttpPost]
    // 虽然这里只读取数据，仍与网页表单保持一致地要求防伪令牌。
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Price(PriceSuggestionViewModel input, CancellationToken cancellationToken)
    {
        // 限制输入范围，错误时返回 400 和可读提示。
        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Choose a category and a valid condition before requesting a price reference." });
        }
        // 不存在的分类不应该被当成正常的“样本不足”。
        bool categoryExists = await _db.Categories.AnyAsync(x => x.Id == input.CategoryId, cancellationToken);
        // 分类已经被删除或编号无效时，要求重新选择。
        if (!categoryExists)
        {
            return BadRequest(new { success = false, message = "Please choose an existing category." });
        }
        // 查询并计算参考金额，不会保存或更改任何商品价格。
        PriceSuggestionResult result = await _assistant.SuggestPriceAsync(input, cancellationToken);
        // JSON 默认使用 camelCase：hasData、sampleCount、suggestedPrice 等。
        return Json(new { success = true, result });
    }
}
