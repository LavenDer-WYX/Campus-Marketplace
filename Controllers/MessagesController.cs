using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

[Authorize]
public class MessagesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;

    public MessagesController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index()
    {
        var userId = _userManager.GetUserId(User);
        var conversations = await _db.Conversations.AsNoTracking()
            .Include(x => x.Listing)
            .Include(x => x.Buyer)
            .Include(x => x.Seller)
            .Include(x => x.Messages)
            .Where(x => x.BuyerId == userId || x.SellerId == userId)
            .OrderByDescending(x => x.Messages.Max(m => (DateTime?)m.SentAt) ?? x.CreatedAt)
            .ToListAsync();

        return View(conversations);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int listingId)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var listing = await _db.Listings.FindAsync(listingId);
        if (listing is null) return NotFound();

        if (listing.SellerId == userId)
        {
            TempData["Message"] = "You cannot message yourself about your own item.";
            return RedirectToAction("Details", "Listings", new { id = listingId });
        }

        var conversation = await _db.Conversations
            .FirstOrDefaultAsync(x => x.ListingId == listingId && x.BuyerId == userId);

        if (conversation is null)
        {
            conversation = new Conversation
            {
                ListingId = listingId,
                BuyerId = userId,
                SellerId = listing.SellerId
            };
            _db.Conversations.Add(conversation);
            await _db.SaveChangesAsync();
        }

        return RedirectToAction(nameof(Chat), new { id = conversation.Id });
    }

    public async Task<IActionResult> Chat(int id)
    {
        var userId = _userManager.GetUserId(User);
        var conversation = await _db.Conversations
            .Include(x => x.Listing)
            .Include(x => x.Buyer)
            .Include(x => x.Seller)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (conversation is null) return NotFound();
        if (conversation.BuyerId != userId && conversation.SellerId != userId) return Forbid();

        var messages = await _db.Messages
            .Include(x => x.Sender)
            .Where(x => x.ConversationId == id)
            .OrderBy(x => x.SentAt)
            .ToListAsync();

        foreach (var message in messages.Where(x => x.SenderId != userId && !x.IsRead))
        {
            message.IsRead = true;
        }
        await _db.SaveChangesAsync();

        return View(new MessagePageViewModel
        {
            Conversation = conversation,
            Messages = messages
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Send(int conversationId, string content)
    {
        var userId = _userManager.GetUserId(User);
        if (userId is null) return Challenge();

        var conversation = await _db.Conversations.FindAsync(conversationId);
        if (conversation is null) return NotFound();
        if (conversation.BuyerId != userId && conversation.SellerId != userId) return Forbid();

        content = content?.Trim() ?? string.Empty;
        if (content.Length == 0 || content.Length > 1000)
        {
            TempData["Message"] = "Message must be between 1 and 1000 characters.";
            return RedirectToAction(nameof(Chat), new { id = conversationId });
        }

        _db.Messages.Add(new Message
        {
            ConversationId = conversationId,
            SenderId = userId,
            Content = content
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Chat), new { id = conversationId });
    }
}
