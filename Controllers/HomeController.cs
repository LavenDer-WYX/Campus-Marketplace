using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers
{
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
                .Where(x => x.IsAvailable && x.Status == "Available")
                .OrderByDescending(x => x.CreatedAt)
                .Take(6)
                .ToListAsync();

            var userId = _userManager.GetUserId(User);
            ViewBag.FavouriteIds = userId is null
                ? new HashSet<int>()
                : (await _db.Favourites.AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .Select(x => x.ListingId)
                    .ToListAsync()).ToHashSet();
            return View(listings);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
