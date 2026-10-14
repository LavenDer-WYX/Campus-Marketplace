using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize]
public class ProfileController : Controller
{
    private readonly UserManager<AppUser> _userManager;
    private readonly ApplicationDbContext _db;

    public ProfileController(UserManager<AppUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }


    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var listings = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.SellerId == user.Id)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        var model = new ProfileViewModel { User = user, Listings = listings };
        return View(model);
    }
}
