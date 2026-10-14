
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.ViewComponents;

public class NotificationCountViewComponent : ViewComponent
{

    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;


    public NotificationCountViewComponent(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }


    public async Task<IViewComponentResult> InvokeAsync()
    {

        var userId = _userManager.GetUserId(HttpContext.User);

        int count = 0;

        if (userId is not null)
        {
            count = await _db.Notifications.CountAsync(x => x.UserId == userId && !x.IsRead);
        }

        return View(count);
    }
}
