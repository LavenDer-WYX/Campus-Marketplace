using Microsoft.AspNetCore.Mvc;

namespace CampusMarketplace1.Controllers;

public class MessagesController : Controller
{
 public IActionResult Chat(int id = 1)
 {
      // 这里只是聊天页面的框架
      ViewData["ConversationId"] = id;

      return View();
 }
}
