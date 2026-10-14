using System.Data;
using Microsoft.Data.SqlClient;
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
        if (userId == null)
        {
            return Challenge();
        }

        var listing = await _db.Listings.FindAsync(listingId);
        if (listing == null || listing.IsHidden)
        {
            return NotFound();
        }
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
        if (listing.ListingType == "Sale")
        {

            TempData["Message"] = "Sale listings use Buy now. Please click Buy now on the item page.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var alreadyPending = await _db.TradeRequests.AnyAsync(x =>
            x.ListingId == listingId && x.BuyerId == userId && x.Status == "Pending");
        if (alreadyPending)
        {
            TempData["Message"] = "You already have a pending request for this item.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        if (string.IsNullOrWhiteSpace(note))
        {
            note = null;
        }
        else
        {
            note = note.Trim();
        }
        if (note != null && note.Length > 500)
        {
            TempData["Message"] = "The note must be 500 characters or shorter.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var requestType = listing.ListingType;
        _db.TradeRequests.Add(new TradeRequest
        {
            ListingId = listingId,
            BuyerId = userId,
            SellerId = listing.SellerId,
            RequestType = requestType,
            Note = note
        });
        AddTradeNotification(listing.SellerId, "You received a new trade request.");
        await _db.SaveChangesAsync();

        TempData["Message"] = "Trade request sent.";
        return RedirectToAction(nameof(Index));
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyNow(int listingId, string? note)
    {
        var userId = _userManager.GetUserId(User);
        if (userId == null)
        {
            return Challenge();
        }



        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var listing = await _db.Listings.FirstOrDefaultAsync(x => x.Id == listingId);
            if (listing == null || listing.IsHidden)
            {
                return NotFound();
            }
            if (listing.SellerId == userId)
            {
                TempData["Message"] = "You cannot buy your own item.";
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }
            if (listing.ListingType != "Sale")
            {
                TempData["Message"] = "Buy now is only available for sale listings.";
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }
            if (!listing.IsAvailable || listing.Status != "Available")
            {
                TempData["Message"] = "Sorry, another buyer has already reserved this item.";
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                note = null;
            }
            else
            {
                note = note.Trim();
            }
            if (note != null && note.Length > 500)
            {
                TempData["Message"] = "The note must be 500 characters or shorter.";
                return RedirectToAction("Details", "Listings", new { id = listingId });
            }

            var purchase = new TradeRequest
            {
                ListingId = listing.Id,
                BuyerId = userId,
                SellerId = listing.SellerId,
                RequestType = "Buy",
                Note = note,
                Status = "Accepted"
            };
            _db.TradeRequests.Add(purchase);

            listing.Status = "Reserved";
            listing.IsAvailable = false;
            listing.UpdatedAt = DateTime.UtcNow;


            var oldRequests = await _db.TradeRequests.Where(x =>
                x.ListingId == listing.Id && x.Status == "Pending").ToListAsync();
            foreach (var oldRequest in oldRequests)
            {
                oldRequest.Status = "Rejected";
                oldRequest.UpdatedAt = DateTime.UtcNow;
                AddTradeNotification(oldRequest.BuyerId,
                    "Your request was declined because another buyer used Buy now.");
            }

            AddTradeNotification(listing.SellerId,
                $"Your item '{listing.Title}' was reserved through Buy now. Please arrange pickup with the buyer.");

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Message"] = "Purchase successful. The item is reserved for you. Contact the seller to arrange payment and pickup.";
            return RedirectToAction(nameof(Index));
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {

            TempData["Message"] = "Another buyer is purchasing this item. Please return to the item page and try again.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {

            TempData["Message"] = "Another buyer is purchasing this item. Please return to the item page and try again.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Accept(int id)
    {
        var userId = _userManager.GetUserId(User);


        await using var transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        try
        {
            var request = await _db.TradeRequests.Include(x => x.Listing)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (request == null)
            {
                return NotFound();
            }
            if (request.SellerId != userId)
            {
                return Forbid();
            }
            if (request.Status != "Pending")
            {
                return RedirectToAction(nameof(Index));
            }

            var listing = request.Listing;
            if (listing == null || listing.IsHidden || !listing.IsAvailable || listing.Status != "Available")
            {
                TempData["Message"] = "This item is no longer available for this request.";
                return RedirectToAction(nameof(Index));
            }

            var alreadyTaken = await _db.TradeRequests.AnyAsync(x =>
                x.ListingId == request.ListingId && x.Id != id &&
                (x.Status == "Accepted" || x.Status == "Completed"));
            if (alreadyTaken)
            {
                TempData["Message"] = "Another trade already reserves or completes this item.";
                return RedirectToAction(nameof(Index));
            }

            request.Status = "Accepted";
            request.UpdatedAt = DateTime.UtcNow;
            listing.Status = "Reserved";
            listing.IsAvailable = false;
            listing.UpdatedAt = DateTime.UtcNow;
            AddTradeNotification(request.BuyerId, "Your trade request was accepted. The item is reserved for you.");

            var otherRequests = await _db.TradeRequests.Where(x =>
                x.ListingId == request.ListingId && x.Id != id && x.Status == "Pending").ToListAsync();
            foreach (var other in otherRequests)
            {
                other.Status = "Rejected";
                other.UpdatedAt = DateTime.UtcNow;
                AddTradeNotification(other.BuyerId, "Your request was declined because this item was reserved for another buyer.");
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            TempData["Message"] = "Request accepted. The item is now reserved.";
        }
        catch (SqlException exception) when (exception.Number == 1205)
        {

            TempData["Message"] = "Another trade was updated at the same time. Please refresh and try again.";
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 1205 })
        {

            TempData["Message"] = "Another trade was updated at the same time. Please refresh and try again.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id)
    {
        var userId = _userManager.GetUserId(User);
        var request = await _db.TradeRequests.FindAsync(id);
        if (request == null)
        {
            return NotFound();
        }
        if (request.SellerId != userId)
        {
            return Forbid();
        }
        if (request.Status == "Pending")
        {
            request.Status = "Rejected";
            request.UpdatedAt = DateTime.UtcNow;
            AddTradeNotification(request.BuyerId, "Your trade request was declined.");
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
        if (request == null)
        {
            return NotFound();
        }
        if (request.BuyerId != userId)
        {
            return Forbid();
        }
        if (request.Status == "Pending")
        {
            request.Status = "Cancelled";
            request.UpdatedAt = DateTime.UtcNow;
            AddTradeNotification(request.SellerId, "A buyer cancelled their pending trade request.");
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
        if (request == null)
        {
            return NotFound();
        }
        if (request.SellerId != userId)
        {
            return Forbid();
        }
        if (request.Status != "Accepted")
        {
            return RedirectToAction(nameof(Index));
        }

        request.Status = "Completed";
        request.UpdatedAt = DateTime.UtcNow;
        if (request.Listing != null)
        {

            if (request.RequestType == "Swap")
            {
                request.Listing.Status = "Swapped";
            }
            else if (request.RequestType == "Giveaway")
            {
                request.Listing.Status = "Given away";
            }
            else
            {
                request.Listing.Status = "Sold";
            }
            if (request.Listing.IsHidden)
            {
                request.Listing.Status = "Unavailable";
            }
            request.Listing.IsAvailable = false;
            request.Listing.UpdatedAt = DateTime.UtcNow;
        }

        AddTradeNotification(request.BuyerId, "Your trade was marked as completed by the seller.");
        await _db.SaveChangesAsync();
        TempData["Message"] = "Trade completed.";
        return RedirectToAction(nameof(Index));
    }



    private void AddTradeNotification(string recipientId, string content)
    {
        if (recipientId == _userManager.GetUserId(User))
        {
            return;
        }

        _db.Notifications.Add(new Notification
        {
            UserId = recipientId,
            Type = "Trade",
            Content = content,
            RelatedUrl = "/TradeRequests"
        });
    }
}
