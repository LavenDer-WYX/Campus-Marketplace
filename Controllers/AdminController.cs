
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.EntityFrameworkCore;

using WebApplication1.Data;

using WebApplication1.Models;


namespace WebApplication1.Controllers;


[Authorize(Policy = "AdminOnly")]
public class AdminController : Controller
{

    private readonly ApplicationDbContext _db;


    private readonly UserManager<AppUser> _userManager;


    public AdminController(ApplicationDbContext db, UserManager<AppUser> userManager)
    {

        _db = db;

        _userManager = userManager;
    }


    [HttpGet]
    public async Task<IActionResult> Index()
    {

        var model = new AdminDashboardViewModel();

        model.UserCount = await _db.Users.CountAsync();

        model.ListingCount = await _db.Listings.CountAsync();

        model.AvailableListingCount = await _db.Listings.CountAsync(item =>
            !item.IsHidden && item.IsAvailable && item.Status == "Available");

        model.CompletedTradeCount = await _db.TradeRequests.CountAsync(request => request.Status == "Completed");

        model.PendingReportCount = await _db.Reports.CountAsync(report => report.Status == "Pending");

        return View(model);
    }


    [HttpGet]
    public async Task<IActionResult> Reports(string? status)
    {

        if (string.IsNullOrWhiteSpace(status))
        {
            status = "Pending";
        }

        if (status != "Pending" && status != "Dismissed" && status != "ActionTaken" && status != "All")
        {
            return BadRequest();
        }


        var query = _db.Reports.AsNoTracking();

        if (status != "All")
        {
            query = query.Where(report => report.Status == status);
        }

        ViewData["Status"] = status;

        var reports = await query.OrderByDescending(report => report.CreatedAt).ToListAsync();

        return View(reports);
    }


    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {

        var report = await _db.Reports.AsNoTracking()
            .Include(item => item.Listing)
            .Include(item => item.Reporter)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (report == null)
        {
            return NotFound();
        }


        var model = new AdminReportActionViewModel();

        model.Id = report.Id;

        model.Report = report;

        model.AdminNote = report.AdminNote;

        return View(model);
    }


    [HttpPost]

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Dismiss([Bind("Id,AdminNote")] AdminReportActionViewModel model)
    {

        return await ResolveReportAsync(model, false);
    }


    [HttpPost]

    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HideListing([Bind("Id,AdminNote")] AdminReportActionViewModel model)
    {

        return await ResolveReportAsync(model, true);
    }



    private async Task<IActionResult> ResolveReportAsync(AdminReportActionViewModel model, bool hideListing)
    {

        var adminId = _userManager.GetUserId(User);

        if (adminId == null)
        {
            return Challenge();
        }


        var report = await _db.Reports
            .Include(item => item.Listing)
            .Include(item => item.Reporter)
            .FirstOrDefaultAsync(item => item.Id == model.Id);

        if (report == null)
        {
            return NotFound();
        }

        if (report.Status != "Pending")
        {
            TempData["Message"] = "This report has already been reviewed.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }


        if (hideListing && string.IsNullOrWhiteSpace(model.AdminNote))
        {
            ModelState.AddModelError(nameof(model.AdminNote), "Explain why this item should be hidden.");
        }

        if (hideListing && report.Listing == null)
        {
            ModelState.AddModelError(string.Empty, "The item was deleted. You can close this report without hiding it.");
        }

        if (!ModelState.IsValid)
        {

            model.Report = report;
            return View("Details", model);
        }

        report.AdminNote = model.AdminNote?.Trim();

        report.ResolvedById = adminId;

        report.ResolvedAt = DateTime.UtcNow;


        if (hideListing && report.Listing != null)
        {

            var wasAlreadyHidden = report.Listing.IsHidden;

            report.Listing.IsHidden = true;

            report.Listing.IsAvailable = false;

            report.Listing.Status = "Unavailable";

            report.Listing.UpdatedAt = DateTime.UtcNow;

            report.Status = "ActionTaken";


            if (!wasAlreadyHidden)
            {

                _db.Notifications.Add(new Notification
                {

                    UserId = report.Listing.SellerId,

                    Type = "Report",

                    Content = "An administrator hid your item: " + report.Listing.Title + ". Please contact the project administrator for details.",

                    RelatedUrl = "/Listings/MyListings"
                });
            }
        }
        else
        {

            report.Status = "Dismissed";
        }


        _db.Notifications.Add(new Notification
        {

            UserId = report.ReporterId,

            Type = "Report",

            Content = "Your report about " + report.ListingTitle + " has been reviewed: " + report.Status + ".",

            RelatedUrl = "/Reports/Index"
        });

        try
        {

            await _db.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {


            TempData["Message"] = "Another administrator already reviewed this report. Please check the latest result.";
            return RedirectToAction(nameof(Details), new { id = report.Id });
        }

        TempData["Message"] = "The report has been reviewed.";

        return RedirectToAction(nameof(Details), new { id = report.Id });
    }
}
