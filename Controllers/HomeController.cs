using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;


public class HomeController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public HomeController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }


    public async Task<IActionResult> Index()
    {
        var listings = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Photos)
            .Where(x => x.IsAvailable && x.Status == "Available" && !x.IsHidden)
            .OrderByDescending(x => x.CreatedAt)
            .Take(6)
            .ToListAsync();

        var userId = _userManager.GetUserId(User);
        var favouriteIds = new HashSet<int>();
        if (userId != null)
        {
            var savedIds = await _db.Favourites.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ListingId)
                .ToListAsync();
            favouriteIds = savedIds.ToHashSet();
        }

        ViewBag.FavouriteIds = favouriteIds;
        return View(listings);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        var requestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        var model = new ErrorViewModel { RequestId = requestId };
        return View(model);
    }
}
