using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;

namespace WebApplication1.Controllers;

public class ListingsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<AppUser> _userManager;
    private readonly IWebHostEnvironment _environment;

    public ListingsController(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        IWebHostEnvironment environment)
    {
        _db = db;
        _userManager = userManager;
        _environment = environment;
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? search, int? categoryId, string? condition, string? listingType)
    {
        var query = _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Photos)
            .Where(x => x.IsAvailable && x.Status == "Available");

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(x => x.Title.Contains(search) || x.Description.Contains(search));
        }
        if (categoryId.HasValue)
        {
            query = query.Where(x => x.CategoryId == categoryId.Value);
        }
        if (!string.IsNullOrWhiteSpace(condition))
        {
            query = query.Where(x => x.Condition == condition);
        }
        if (!string.IsNullOrWhiteSpace(listingType))
        {
            query = query.Where(x => x.ListingType == listingType);
        }

        await SetCategoryOptionsAsync(categoryId);
        ViewData["Search"] = search;
        ViewData["Condition"] = condition;
        ViewData["ListingType"] = listingType;
        var userId = _userManager.GetUserId(User);
        ViewBag.FavouriteIds = userId is null
            ? new HashSet<int>()
            : (await _db.Favourites.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ListingId)
                .ToListAsync()).ToHashSet();
        return View(await query.OrderByDescending(x => x.CreatedAt).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var listing = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Include(x => x.Seller)
            .Include(x => x.Photos)
            .FirstOrDefaultAsync(x => x.Id == id);

        if (listing is null) return NotFound();

        var userId = _userManager.GetUserId(User);
        ViewBag.IsOwner = userId == listing.SellerId;
        ViewBag.IsFavourite = userId is not null &&
            await _db.Favourites.AnyAsync(x => x.UserId == userId && x.ListingId == id);
        ViewBag.HasPendingRequest = userId is not null &&
            await _db.TradeRequests.AnyAsync(x =>
                x.ListingId == id && x.BuyerId == userId && x.Status == "Pending");

        return View(listing);
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        await SetCategoryOptionsAsync();
        return View(new Listing());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Title,CategoryId,Description,Condition,Price,ListingType,PickupLocation,IsAvailable")] Listing listing,
        List<IFormFile>? photos)
    {
        ModelState.Remove(nameof(Listing.SellerId));
        ValidatePhotos(photos, 0);
        if (listing.ListingType == "Sale" && listing.Price is null)
        {
            ModelState.AddModelError(nameof(Listing.Price), "A price is required for sale listings.");
        }
        else if (listing.ListingType != "Sale")
        {
            listing.Price = null;
        }

        if (!ModelState.IsValid)
        {
            await SetCategoryOptionsAsync(listing.CategoryId);
            return View(listing);
        }

        var userId = _userManager.GetUserId(User);
        if (userId is null)
        {
            return Challenge();
        }

        listing.SellerId = userId;
        listing.Status = "Available";
        listing.CreatedAt = DateTime.UtcNow;
        listing.UpdatedAt = DateTime.UtcNow;
        _db.Listings.Add(listing);
        await _db.SaveChangesAsync();
        await SavePhotosAsync(listing.Id, photos);
        return RedirectToAction(nameof(Details), new { id = listing.Id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var listing = await _db.Listings.FindAsync(id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();

        await SetCategoryOptionsAsync(listing.CategoryId);
        return View(listing);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Title,CategoryId,Description,Condition,Price,ListingType,PickupLocation,Status,IsAvailable")] Listing input,
        List<IFormFile>? photos)
    {
        var listing = await _db.Listings.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();

        ModelState.Remove(nameof(Listing.SellerId));
        ValidatePhotos(photos, listing.Photos.Count);
        if (input.ListingType == "Sale" && input.Price is null)
        {
            ModelState.AddModelError(nameof(Listing.Price), "A price is required for sale listings.");
        }
        else if (input.ListingType != "Sale")
        {
            input.Price = null;
        }

        if (!ModelState.IsValid)
        {
            input.Id = id;
            await SetCategoryOptionsAsync(input.CategoryId);
            return View(input);
        }

        listing.Title = input.Title;
        listing.CategoryId = input.CategoryId;
        listing.Description = input.Description;
        listing.Condition = input.Condition;
        listing.Price = input.Price;
        listing.ListingType = input.ListingType;
        listing.PickupLocation = input.PickupLocation;
        listing.Status = input.Status;
        listing.IsAvailable = input.IsAvailable;
        listing.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        await SavePhotosAsync(listing.Id, photos);
        return RedirectToAction(nameof(Details), new { id });
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var listing = await _db.Listings.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        return CanManage(listing) ? View(listing) : Forbid();
    }

    [Authorize]
    [HttpPost, ActionName("Delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var listing = await _db.Listings.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (listing is null) return NotFound();
        if (!CanManage(listing)) return Forbid();

        DeletePhotoFiles(listing.Photos);
        _db.Listings.Remove(listing);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(MyListings));
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyListings()
    {
        var userId = _userManager.GetUserId(User);
        var listings = await _db.Listings.AsNoTracking()
            .Include(x => x.Category)
            .Where(x => x.SellerId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .ToListAsync();
        return View(listings);
    }

    private bool CanManage(Listing listing) =>
        listing.SellerId == _userManager.GetUserId(User) || User.IsInRole(UserRoles.Admin);

    private async Task SetCategoryOptionsAsync(int? selectedId = null)
    {
        ViewBag.Categories = new SelectList(
            await _db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync(),
            nameof(Category.Id), nameof(Category.Name), selectedId);
    }

    private void ValidatePhotos(List<IFormFile>? photos, int currentPhotoCount)
    {
        if (photos is null || photos.Count == 0) return;

        if (currentPhotoCount + photos.Count > 5)
        {
            ModelState.AddModelError("photos", "Each listing can have up to 5 photos.");
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
        var allowedContentTypes = new[] { "image/jpeg", "image/png", "image/webp" };

        foreach (var photo in photos)
        {
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension) || !allowedContentTypes.Contains(photo.ContentType))
            {
                ModelState.AddModelError("photos", "Only JPG, PNG and WebP images are allowed.");
            }
            if (photo.Length == 0 || photo.Length > 5 * 1024 * 1024)
            {
                ModelState.AddModelError("photos", "Each photo must be smaller than 5 MB.");
            }
        }
    }

    private async Task SavePhotosAsync(int listingId, List<IFormFile>? photos)
    {
        if (photos is null || photos.Count == 0) return;

        var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        Directory.CreateDirectory(uploadFolder);

        foreach (var photo in photos)
        {
            var extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadFolder, fileName);

            await using var stream = System.IO.File.Create(filePath);
            await photo.CopyToAsync(stream);

            _db.ListingPhotos.Add(new ListingPhoto
            {
                ListingId = listingId,
                ImageUrl = $"/uploads/listings/{fileName}"
            });
        }

        await _db.SaveChangesAsync();
    }

    private void DeletePhotoFiles(IEnumerable<ListingPhoto> photos)
    {
        var uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        foreach (var photo in photos)
        {
            var fileName = Path.GetFileName(photo.ImageUrl);
            var filePath = Path.Combine(uploadFolder, fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
