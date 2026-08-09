using Microsoft.AspNetCore.Mvc;

namespace CampusMarketplace1.Controllers;

public class AccountController : Controller
{
  public IActionResult Login()
  {
      // 现在只打开登录页面，不处理登录数据
      return View();
  }
}
