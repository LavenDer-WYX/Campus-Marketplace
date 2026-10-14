using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize]
public class FavouritesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public FavouritesController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var favourites = await _db.Favourites.AsNoTracking()
            .Include(x => x.Listing).ThenInclude(x => x!.Category)
            .Include(x => x.Listing).ThenInclude(x => x!.Photos)
            .Where(x => x.UserId == userId && x.Listing != null && !x.Listing.IsHidden)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(favourites);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int listingId, string? returnUrl = null)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }
        var listing = await _db.Listings.FindAsync(listingId);
        if (listing == null)
        {
            return NotFound();
        }

        var favourite = await _db.Favourites.FindAsync(userId, listingId);
        if (favourite == null)
        {
            if (listing.IsHidden)
            {
                return NotFound();
            }

            _db.Favourites.Add(new Favourite { UserId = userId, ListingId = listingId });
            TempData["Message"] = "Added to favourites.";
        }
        else
        {
            _db.Favourites.Remove(favourite);
            TempData["Message"] = "Removed from favourites.";
        }

        await _db.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        if (listing.IsHidden)
        {
            return RedirectToAction(nameof(Index));
        }

        return RedirectToAction("Details", "Listings", new { id = listingId });
    }
}
