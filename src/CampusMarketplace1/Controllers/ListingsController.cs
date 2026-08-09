using Microsoft.AspNetCore.Mvc;

namespace CampusMarketplace1.Controllers;

public class ListingsController : Controller
{
  public IActionResult Details(int id = 1)
  {
       // 先把编号放到页面上，之后再连接数据库
       ViewData["ListingId"] = id;

       return View();
  }
}
