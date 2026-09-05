using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize]
public class TradeRequestsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public TradeRequestsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var requests = await _db.TradeRequests.AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Buyer)
            .Include(x => x.Seller)
            .Where(x => x.BuyerId == userId || x.SellerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();

        return View(requests);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(int listingId, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var listing = await _db.Listings.FindAsync(listingId);
        if (listing is null) return NotFound();
        if (listing.SellerId == userId)
        {
            TempData["Message"] = "You cannot request your own item.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }
        if (!listing.IsAvailable || listing.Status != "Available")
        {
            TempData["Message"] = "This item is not available now.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var alreadyPending = await _db.TradeRequests.AnyAsync(x =>
            x.ListingId == listingId && x.BuyerId == userId && x.Status == "Pending");
        if (alreadyPending)
        {
            TempData["Message"] = "You already have a pending request for this item.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (note?.Length > 500)
        {
            TempData["Message"] = "The note must be 500 characters or shorter.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var requestType = listing.ListingType == "Sale" ? "Buy" : listing.ListingType;
        _db.TradeRequests.Add(new TradeRequest
        {
            ListingId = listingId,
            BuyerId = userId,
            SellerId = listing.SellerId,
            RequestType = requestType,
            Note = note
        });
        await _db.SaveChangesAsync();

        TempData["Message"] = "Trade request sent.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = _userManager.GetUserId(User);
        var request = await _db.TradeRequests.Include(x => x.Listing)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (request is null) return NotFound();
        if (request.SellerId != userId) return Forbid();
        if (request.Status != "Pending") return RedirectToAction(nameof(Index));

        request.Status = "Accepted";
        request.UpdatedAt = DateTime.UtcNow;
        if (request.Listing is not null)
        {
            request.Listing.Status = "Reserved";
            request.Listing.IsAvailable = false;
            request.Listing.UpdatedAt = DateTime.UtcNow;
        }

        var otherRequests = await _db.TradeRequests.Where(x =>
            x.ListingId == request.ListingId && x.Id != id && x.Status == "Pending").ToListAsync();
        foreach (var other in otherRequests)
        {
            other.Status = "Rejected";
            other.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Request accepted. The item is now reserved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = _userManager.GetUserId(User);
        var request = await _db.TradeRequests.FindAsync(id);
        if (request is null) return NotFound();
        if (request.SellerId != userId) return Forbid();
        if (request.Status == "Pending")
        {
            request.Status = "Rejected";
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var userId = _userManager.GetUserId(User);
        var request = await _db.TradeRequests.FindAsync(id);
        if (request is null) return NotFound();
        if (request.BuyerId != userId) return Forbid();
        if (request.Status == "Pending")
        {
            request.Status = "Cancelled";
            request.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var userId = _userManager.GetUserId(User);
        var request = await _db.TradeRequests.Include(x => x.Listing)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (request is null) return NotFound();
        if (request.SellerId != userId) return Forbid();
        if (request.Status != "Accepted") return RedirectToAction(nameof(Index));

        request.Status = "Completed";
        request.UpdatedAt = DateTime.UtcNow;
        if (request.Listing is not null)
        {
            request.Listing.Status = request.RequestType switch
            {
                "Swap" => "Swapped",
                "Giveaway" => "Given away",
                _ => "Sold"
            };
            request.Listing.IsAvailable = false;
            request.Listing.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
        TempData["Message"] = "Trade completed.";
        return RedirectToAction(nameof(Index));
    }
}
