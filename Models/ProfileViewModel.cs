namespace WebApplication1.Models;

// 个人资料页同时需要一个用户和多件商品，所以用 ViewModel 打包。
public class ProfileViewModel
{
    // required 要求创建对象时必须赋值；init 表示创建后不再替换。
    public required AppUser User { get; init; }
    // IReadOnlyList 表示页面只读取，不负责增删数据库商品。
    public IReadOnlyList<Listing> Listings { get; init; } = [];
}
