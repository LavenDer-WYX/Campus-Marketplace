# Campus Marketplace 后端初学者说明

这份说明对应当前 Week 8 整合项目。阅读代码时建议从 `Program.cs` 开始，然后看 `Models`、`Data/ApplicationDbContext.cs`、`Controllers`，最后看 `Views`。代码里的中文注释解释单行作用，这份文件解释文件之间怎样合作。

## 一次网页请求怎样运行

以打开商品 5 为例：

```text
浏览器请求 /Listings/Details/5
        ↓
Program.cs 的路由找到 ListingsController
        ↓
Details(int id) 收到 id = 5
        ↓
ApplicationDbContext 让 EF Core 生成 SQL 查询
        ↓
SQL Server 返回 Listing、Category、Seller、Photos
        ↓
Controller 检查商品是否隐藏以及当前用户权限
        ↓
Views/Listings/Details.cshtml 生成 HTML
        ↓
浏览器显示网页
```

Controller 不直接保存数据文件，View 也不直接查询数据库。DbContext 是 C# 与 SQL Server 之间的桥。

## Program.cs

`WebApplication.CreateBuilder(args)` 创建网站配置器并读取 `appsettings.json`、开发配置、User Secrets 和环境变量。

`AddControllersWithViews()` 开启 MVC。`AddDbContext<ApplicationDbContext>()` 告诉程序怎样连接 SQL Server。`AddDefaultIdentity<AppUser>()` 开启注册、密码哈希、登录 Cookie 和角色。

`AddAuthorization()` 定义 `AdminOnly`。Controller 写 `[Authorize(Policy = "AdminOnly")]` 后，普通学生即使手动输入 `/Admin` 也无法访问。

`AddHttpClient<ListingAssistantService>()` 和 `AddHttpClient<SmartSearchService>()` 分别创建描述助手与智能搜索的 AI 请求客户端，都设置 20 秒超时、64 KB 响应上限并禁止自动重定向。`AddRateLimiter()` 让两种 AI 请求共用每位用户每分钟 5 次的限制。

程序启动时运行 `MigrateAsync()`，只应用尚未运行的 Migration，不会每次清空数据库。之后创建 `Student` 和 `Admin` 两个角色。

## appsettings.json

`CampusMarketDb` 是 SQL Server LocalDB 连接字符串。数据库名仍为 `MarketPlaceWeek7Db`，Week 8 Migration 会在原数据库上增加表和字段。

`ListingAssistant` 有三项配置：

- `Endpoint`：豆包所在的火山方舟 Chat Completions HTTPS 地址。
- `Model`：项目默认使用价格较低的 `doubao-seed-2-0-lite-260428`。如果账户使用其他模型，可以用 User Secrets 覆盖；自定义推理接入点则填写 `ep-` 开头的 Endpoint ID。
- `ApiKey`：必须保持为空，不要把真实密钥提交到 GitHub。

开发机配置示例：

```powershell
dotnet user-secrets set "ListingAssistant:ApiKey" "你的火山方舟API Key"
```

如果账户使用的不是默认模型，再执行 `dotnet user-secrets set "ListingAssistant:Model" "控制台中显示的Model ID或Endpoint ID"`。请求体将 `thinking` 设为 `disabled`，因为描述草稿、价格建议和搜索条件提取都不需要深度思考，这样返回更快并减少 token 消耗。没有配置密钥时，AI 描述与价格按钮返回明确提示；智能搜索自动退回普通关键词搜索；用户仍可手动填写描述和价格。

## Models 与数据库表

`AppUser` 继承 Identity 的 `IdentityUser`，所以自动拥有 `Id`、`Email`、`UserName` 和 `PasswordHash`。项目额外保存学号、昵称、校区和简单学校邮箱标记。当前 `IsSchoolVerified` 只表示邮箱后缀合格，并不等于真实邮件确认。

`Category` 是分类。`Listing` 是商品中心实体，关联卖家、分类、照片、收藏、聊天和交易请求。`IsHidden` 由管理员下架时设置，普通编辑表单不能提交这个字段。

`ListingPhoto` 只在数据库保存 URL。实际图片文件放在 `wwwroot/uploads/listings`。

`Conversation` 是买家围绕商品与卖家建立的一次聊天。`Message` 是聊天中的一条文字。打开聊天页时，对方发送的未读消息会被标记为已读。

`Favourite` 使用 `UserId + ListingId` 作为联合主键，同一用户不能重复收藏同一商品。

`TradeRequest` 保存 Buy、Swap 或 Giveaway 请求。状态从 `Pending` 变成 `Accepted`、`Rejected`、`Cancelled` 或 `Completed`。

`Notification` 保存站内通知。通知只保存简短提醒和本站链接，不复制完整聊天内容。用户查询时总是加上自己的 `UserId` 条件。

`Report` 保存商品举报、管理员处理说明和处理状态。它额外保存 `ListingTitle` 快照，所以商品删除以后仍然知道举报对象。`ListingId` 允许变成空值来保留历史。

`RegisterViewModel`、`LoginViewModel`、`ProfileViewModel`、`MessagePageViewModel`、`AdminDashboardViewModel` 等是页面传输对象。它们帮助页面接收或显示数据，不会自动创建数据库表。

## ApplicationDbContext.cs

`ApplicationDbContext : IdentityDbContext<AppUser>` 表示同一个 DbContext 同时管理 Identity 账号表和市场业务表。

每个 `DbSet` 是一张表的 C# 入口，例如 `_db.Listings` 对应 `Listings`。`Add()` 只让 EF Core 准备新增；`SaveChangesAsync()` 才真正执行 SQL。

`OnModelCreating()` 配置关系、索引和删除规则：

- `Restrict` 防止删除用户后留下找不到买卖家的数据。
- `Cascade` 表示删除商品或会话时，一起删除依赖记录。
- `SetNull` 表示删除商品后只清空举报的 `ListingId`，保留举报本身。
- 唯一索引阻止重复学号、重复分类名以及重复待处理举报。
- `Status` 并发检查阻止两位管理员同时处理同一举报并重复发送通知。

## Controller 说明

`AccountController` 负责注册、登录、退出和无权限页面。`UserManager.CreateAsync()` 哈希密码并保存账号，`SignInManager` 创建或清除登录 Cookie。注册页面永远不能选择 Admin 角色。

`HomeController` 查询最新六件公开商品。首页 JavaScript 只过滤这六件已经加载的商品；完整数据库搜索在 `ListingsController.Index()`。

`ListingsController` 负责商品增删改查：

- `Index` 查询公开商品并应用关键词、分类、成色和类型条件。
- `Details` 查询一件商品；隐藏商品只让主人和管理员查看。
- `Create` 从登录身份设置 `SellerId`，检查分类、价格和照片，再保存商品。
- `Edit` 逐项复制允许修改的字段。它不接受 `SellerId` 和 `IsHidden`，也不会把已接受交易的商品重新开放。
- `Delete` 先检查所有权，再清理真实图片并删除数据库记录。
- `MyListings` 显示自己的全部记录，包括管理员隐藏的记录，方便知道发生了什么。

`FavouritesController` 负责添加和取消收藏。隐藏商品不会显示，也不能新收藏，但用户仍可以删除过去的收藏记录。

`MessagesController` 负责建立会话、显示聊天和发送消息。每次读取或发送都检查当前用户是不是会话买家或卖家。发送消息时在同一次 `SaveChangesAsync()` 中保存消息和对方通知。

`TradeRequestsController` 负责交易状态。Sale 商品使用 `BuyNow()`：买家点击后直接建立 `Accepted` 交易并把商品改成 `Reserved`，不需要卖家再接受。这个操作使用 Serializable 数据库事务，因此两名买家同时点击时只有第一名能成功。Swap 和 Giveaway 仍由 `Create()` 建立 `Pending` 请求，等待卖家处理。接受请求时会自动拒绝同一商品的其他待处理请求。状态变化和相关通知一起保存。管理员隐藏的商品不能再建立新交易。

`ProfileController` 把当前 `AppUser` 和他的商品放进 `ProfileViewModel`，交给个人资料页面。

`NotificationsController` 只查询当前用户的通知。用户可以标记单条或全部已读。`NotificationCountViewComponent` 是导航栏的小型查询组件，只返回未读数量。

`ReportsController` 让学生举报别人的公开商品，并查看自己的举报历史。GET 显示表单，POST 才保存数据。POST 会再次检查商品、用户身份和重复举报，不能只相信隐藏输入框。

`AdminController` 使用 `AdminOnly`，显示统计、举报列表和详情。管理员可以关闭举报，或隐藏违规商品。隐藏时同时更新商品、举报和通知。

`ListingsController.SmartSearch` 接收登录用户的一句自然语言，把它交给 `SmartSearchService` 解析；随后仍由 `Index` 使用 EF Core 查询真实商品。AI 不会直接生成商品，也不会直接拼接 SQL。

`ListingAssistantController` 提供两个 POST 接口。`Generate` 调用外部 AI 生成可编辑草稿；`Price` 把当前商品资料和本地匿名价格统计交给 AI，返回建议价格、范围和理由。两个接口都需要登录、防伪令牌，并共用每位用户每分钟 5 次的限流。

## AI 描述、智能搜索与价格建议

`ListingAssistantService.GenerateDescriptionAsync()` 只发送商品标题、分类、成色、使用时间和配件。它不读取邮箱、学号、密码或聊天内容。返回内容先显示为草稿，用户必须点击 `Use this draft` 才会复制到描述框，仍需检查后再提交商品。

`SmartSearchService.ParseAsync()` 只发送用户输入的搜索句子和数据库现有分类名称。豆包返回关键词、分类、价格、成色、地点、商品类型、发布时间和排序方式的 JSON。服务器再次使用分类、成色、类型和数值范围白名单检查；检查后的条件才会进入 `ListingsController.Index`，由 EF Core 参数化查询数据库。页面显示 “AI understood” 说明，用户可以检查 AI 是否理解正确。AI 不可用时退回普通关键词搜索。

外部服务的返回格式按 Chat Completions 的 `choices[0].message.content` 读取。代码处理未配置、凭据错误、限额、超时、非 JSON、空回答和异常条件。项目不提交真实密钥，也不会把邮箱、密码、学号、聊天或完整用户资料发送给搜索模型。

`SuggestPriceAsync()` 先在本地查询最近最多 30 个未隐藏的同分类、同成色、正价格 Sale 商品。没有同成色样本时才退回同分类全部成色。服务器只计算样本数量、平均价、最低价和最高价，不会把其他用户的商品标题或描述发给外部服务。豆包根据当前卖家填写的标题、分类、成色、使用时间、配件和这些匿名统计数字，返回建议价格、建议范围和简短理由。服务器会检查价格必须为正数、范围顺序正确且不超过上限；用户仍要点击 `Use suggested price` 才会写入价格框。它只是挂牌价建议，不代表真实成交价格或专业估值。

## 举报和管理员流程

```text
学生打开商品 → Report this listing → 填原因 → Reports 表新增 Pending
                                                    ↓
管理员 /Admin → 查看详情 → Dismiss 或 Hide listing
                                                    ↓
更新 Report 状态 + 必要时 Listing.IsHidden=true + 生成 Notification
```

普通学生无法仅靠隐藏导航链接进入后台；真正保护来自 `[Authorize(Policy = "AdminOnly")]`。

## 怎样准备管理员账号

先在网页注册一个真实测试账号，然后停止网站，在项目目录运行：

```powershell
dotnet run -- --make-admin 已注册的邮箱地址
```

命令只给明确指定的已有账号添加 Admin 角色，不创建默认密码。完成后重新登录，新的 Cookie 才会带上 Admin 角色，然后访问 `/Admin`。

## Migration

旧 Migration 按顺序建立 Identity、商品、收藏、聊天和交易。新增的 `AddWeek8AdminReportsNotifications`：

- 给 Listings 增加 `IsHidden`。
- 创建 Notifications。
- 创建 Reports。
- 增加通知查询和重复举报保护索引。

`.Designer.cs` 和 `ApplicationDbContextModelSnapshot.cs` 是 EF Core 自动生成的结构描述。它们很长，但不是手写业务流程。学习时看主 Migration 的 `Up()` 和 `Down()` 即可。

## 保留复杂检查的原因

代码已经把难懂的短写法适当展开，但登录身份检查、所有权检查、防伪令牌、数据验证、数据库事务和并发检查不能删除。它们分别防止冒充别人、修改别人的数据、伪造请求、写入坏数据、半完成交易和重复处理举报。

## 建议阅读顺序

1. `Program.cs`
2. `Models/AccountViewModels.cs` 和 `Models/AppUser.cs`
3. `Controllers/AccountController.cs`
4. `Models/Listing.cs` 和 `Data/ApplicationDbContext.cs`
5. `Controllers/ListingsController.cs`
6. `Conversation`、`Message` 和 `MessagesController`
7. `TradeRequest` 和 `TradeRequestsController`
8. `Notification`、`Report`、`NotificationsController`、`ReportsController`
9. `AdminController`
10. `SmartSearchViewModels`、`SmartSearchService` 和 `ListingsController.SmartSearch`
11. `ListingAssistantService`

每次阅读一个 Action，可以按“输入是什么、身份怎么取得、查询了什么、检查了什么、保存了什么、最后跳到哪里”六个问题逐行看。
