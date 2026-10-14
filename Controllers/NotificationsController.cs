
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;


[Authorize]
public class NotificationsController : Controller
{

    private readonly ApplicationDbContext _db;

    private readonly UserManager<AppUser> _userManager;


    public NotificationsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {

        _db = db;
        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Index(int page = 1)
    {

        page = Math.Clamp(page, 1, 10000);

        var userId = _userManager.GetUserId(User);

        if (userId is null) return Challenge();

        var notifications = await _db.Notifications.AsNoTracking()
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .Skip((page - 1) * 30)
            .Take(31)
            .ToListAsync();

        ViewBag.HasNext = notifications.Count > 30;
        ViewBag.Page = page;

        return View(notifications.Take(30).ToList());
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAsRead(int id)
    {

        var userId = _userManager.GetUserId(User);

        var notification = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);

        if (notification is null) return NotFound();

        notification.IsRead = true;
        await _db.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllAsRead()
    {

        var userId = _userManager.GetUserId(User);

        await _db.Notifications.Where(x => x.UserId == userId && !x.IsRead)
            .ExecuteUpdateAsync(update => update.SetProperty(x => x.IsRead, true));

        return RedirectToAction(nameof(Index));
    }
}
