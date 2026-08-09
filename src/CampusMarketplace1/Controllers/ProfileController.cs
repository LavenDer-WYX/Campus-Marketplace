using Microsoft.AspNetCore.Mvc;

namespace CampusMarketplace1.Controllers
{
 public class ProfileController : Controller
 {
      public IActionResult Index()
      {
          // 第二周先显示空白的个人资料框架
          return View();
      }
 }
}
