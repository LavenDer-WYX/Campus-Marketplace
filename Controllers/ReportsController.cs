
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using WebApplication1.Data;

using WebApplication1.Models;


namespace WebApplication1.Controllers;


[Authorize]
public class ReportsController : Controller
{

    private readonly ApplicationDbContext _db;


    private readonly UserManager<AppUser> _userManager;


    public ReportsController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {

        _db = db;

        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {

        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }


        var reports = await _db.Reports.AsNoTracking()
            .Where(report => report.ReporterId == userId)
            .OrderByDescending(report => report.CreatedAt)
            .ToListAsync();

        return View(reports);
    }


    [HttpGet]
    public async Task<IActionResult> Create(int listingId)
    {

        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }


        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == listingId);

        if (listing == null || listing.IsHidden)
        {
            return NotFound();
        }

        if (listing.SellerId == userId)
        {
            return Forbid();
        }


        if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
            report.ListingId == listingId && report.Status == "Pending"))
        {

            TempData["Message"] = "You already have a pending report for this item.";

            return RedirectToAction(nameof(Index));
        }


        var model = new ReportCreateViewModel();

        model.ListingId = listing.Id;

        model.ListingTitle = listing.Title;

        return View(model);
    }


    [HttpPost]

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("ListingId,Reason")] ReportCreateViewModel model)
    {

        var userId = _userManager.GetUserId(User);

        if (userId == null)
        {
            return Challenge();
        }


        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(item => item.Id == model.ListingId);

        if (listing == null || listing.IsHidden)
        {
            return NotFound();
        }

        if (listing.SellerId == userId)
        {
            return Forbid();
        }

        model.ListingTitle = listing.Title;

        if (!ModelState.IsValid)
        {
            return View(model);
        }


        if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
            report.ListingId == model.ListingId && report.Status == "Pending"))
        {

            ModelState.AddModelError(string.Empty, "You already have a pending report for this item.");
            return View(model);
        }


        var newReport = new Report();

        newReport.ReporterId = userId;

        newReport.ListingId = listing.Id;

        newReport.ListingTitle = listing.Title;

        newReport.Reason = model.Reason.Trim();

        newReport.Status = "Pending";

        newReport.CreatedAt = DateTime.UtcNow;

        _db.Reports.Add(newReport);


        try
        {

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {

            _db.Entry(newReport).State = EntityState.Detached;

            if (await _db.Reports.AnyAsync(report => report.ReporterId == userId &&
                report.ListingId == model.ListingId && report.Status == "Pending"))
            {
                ModelState.AddModelError(string.Empty, "You already have a pending report for this item.");
                return View(model);
            }

            throw;
        }


        TempData["Message"] = "Your report has been submitted for review.";

        return RedirectToAction(nameof(Index));
    }
}
