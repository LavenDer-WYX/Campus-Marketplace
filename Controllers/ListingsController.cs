
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;

using Microsoft.EntityFrameworkCore;
using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;


public class ListingsController : Controller
{

    private readonly ApplicationDbContext _db;

    private readonly UserManager<AppUser> _userManager;

    private readonly IWebHostEnvironment _environment;

    private readonly SmartSearchService _smartSearch;


    public ListingsController(
        ApplicationDbContext db,
        UserManager<AppUser> userManager,
        IWebHostEnvironment environment,
        SmartSearchService smartSearch)
    {

        _db = db;
        _userManager = userManager;
        _environment = environment;
        _smartSearch = smartSearch;
    }


    [HttpGet]
    public async Task<IActionResult> Index(
        string? search,
        int? categoryId,
        string? condition,
        string? listingType,
        decimal? minimumPrice,
        decimal? maximumPrice,
        string? location,
        int? postedWithinDays,
        string? sortBy,
        string? smartQuery)
    {

        var query = _db.Listings.AsNoTracking()

            .Include(x => x.Category)
            .Include(x => x.Photos)

            .Where(x => x.IsAvailable && x.Status == "Available" && !x.IsHidden);


        if (!string.IsNullOrWhiteSpace(search))
        {

            search = search.Trim();
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

        if (minimumPrice is >= 0 and <= 100000)
        {
            query = query.Where(x => x.Price.HasValue && x.Price >= minimumPrice.Value);
        }
        if (maximumPrice is >= 0 and <= 100000)
        {
            query = query.Where(x => x.Price.HasValue && x.Price <= maximumPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(location))
        {
            location = location.Trim();
            if (location.Length > 100) location = location[..100];
            query = query.Where(x => x.PickupLocation.Contains(location));
        }

        if (postedWithinDays is >= 1 and <= 365)
        {
            DateTime earliestDate = DateTime.UtcNow.AddDays(-postedWithinDays.Value);
            query = query.Where(x => x.CreatedAt >= earliestDate);
        }


        await SetCategoryOptionsAsync(categoryId);

        ViewData["Search"] = search;
        ViewData["Condition"] = condition;
        ViewData["ListingType"] = listingType;
        ViewData["MinimumPrice"] = minimumPrice;
        ViewData["MaximumPrice"] = maximumPrice;
        ViewData["Location"] = location;
        ViewData["PostedWithinDays"] = postedWithinDays;
        ViewData["SortBy"] = sortBy;
        ViewData["SmartQuery"] = smartQuery;
        ViewData["SmartSearchMessage"] = TempData["SmartSearchMessage"];
        ViewData["SmartSearchError"] = TempData["SmartSearchError"];


        var userId = _userManager.GetUserId(User);
        var favouriteIds = new HashSet<int>();

        if (userId is not null)
        {
            var ids = await _db.Favourites.AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => x.ListingId)
                .ToListAsync();

            favouriteIds = ids.ToHashSet();
        }
        ViewBag.FavouriteIds = favouriteIds;


        var listings = sortBy == "price_low_to_high"
            ? await query.OrderBy(x => x.Price == null).ThenBy(x => x.Price).ThenByDescending(x => x.CreatedAt).ToListAsync()
            : await query.OrderByDescending(x => x.CreatedAt).ToListAsync();
        return View(listings);
    }


    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("listing-assistant")]
    public async Task<IActionResult> SmartSearch(
        SmartSearchInputViewModel input,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            TempData["SmartSearchError"] = "Enter a search request between 3 and 300 characters.";
            return RedirectToAction(nameof(Index));
        }

        string userQuery = input.Query.Trim();

        var categories = await _db.Categories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new { x.Id, x.Name })
            .ToListAsync(cancellationToken);

        SmartSearchResult result = await _smartSearch.ParseAsync(
            userQuery,
            categories.Select(x => x.Name).ToArray(),
            cancellationToken);


        if (!result.Success || result.Filters is null)
        {
            TempData["SmartSearchError"] = result.Message;
            return RedirectToAction(nameof(Index), new { search = userQuery, smartQuery = userQuery });
        }

        SmartSearchFilters filters = result.Filters;
        int? categoryId = categories.FirstOrDefault(x =>
            string.Equals(x.Name, filters.Category, StringComparison.OrdinalIgnoreCase))?.Id;


        var understood = new List<string>();
        if (filters.Keyword is not null) understood.Add("keyword: " + filters.Keyword);
        if (filters.Category is not null) understood.Add("category: " + filters.Category);
        if (filters.MinimumPrice.HasValue) understood.Add("minimum price: NZ$" + filters.MinimumPrice.Value.ToString("0.##"));
        if (filters.MaximumPrice.HasValue) understood.Add("maximum price: NZ$" + filters.MaximumPrice.Value.ToString("0.##"));
        if (filters.Condition is not null) understood.Add("condition: " + filters.Condition);
        if (filters.Location is not null) understood.Add("location: " + filters.Location);
        if (filters.ListingType is not null) understood.Add("type: " + filters.ListingType);
        if (filters.PostedWithinDays.HasValue) understood.Add("posted within: " + filters.PostedWithinDays.Value + " days");
        if (filters.SortBy == "price_low_to_high") understood.Add("sort: lowest price first");

        TempData["SmartSearchMessage"] = understood.Count == 0
            ? "AI did not find a specific filter, so all available listings are shown."
            : "AI understood: " + string.Join("; ", understood) + ". Results below come from the local database.";

        return RedirectToAction(nameof(Index), new
        {
            search = filters.Keyword,
            categoryId,
            condition = filters.Condition,
            listingType = filters.ListingType,
            minimumPrice = filters.MinimumPrice,
            maximumPrice = filters.MaximumPrice,
            location = filters.Location,
            postedWithinDays = filters.PostedWithinDays,
            sortBy = filters.SortBy,
            smartQuery = userQuery
        });
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
        bool isOwner = userId == listing.SellerId;
        bool isAdmin = User.IsInRole(UserRoles.Admin);

        if (listing.IsHidden && !isOwner && !isAdmin) return NotFound();


        ViewBag.IsOwner = isOwner;
        ViewBag.IsFavourite = userId is not null && await _db.Favourites
            .AnyAsync(x => x.UserId == userId && x.ListingId == id);
        ViewBag.HasPendingRequest = userId is not null && await _db.TradeRequests
            .AnyAsync(x => x.ListingId == id && x.BuyerId == userId && x.Status == "Pending");
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

        if (!await _db.Categories.AnyAsync(x => x.Id == listing.CategoryId))
        {
            ModelState.AddModelError(nameof(Listing.CategoryId), "Please choose an existing category.");
        }

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
        if (userId is null) return Challenge();
        listing.SellerId = userId;

        listing.Status = "Available";
        listing.IsHidden = false;
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

        if (!await _db.Categories.AnyAsync(x => x.Id == input.CategoryId))
        {
            ModelState.AddModelError(nameof(Listing.CategoryId), "Please choose an existing category.");
        }
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
            input.IsHidden = listing.IsHidden;
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


        bool hasActiveTrade = await _db.TradeRequests.AnyAsync(x => x.ListingId == id &&
            (x.Status == "Accepted" || x.Status == "Completed"));
        if (listing.IsHidden)
        {

            listing.Status = "Unavailable";
            listing.IsAvailable = false;
        }
        else if (!hasActiveTrade)
        {

            listing.Status = input.Status;

            listing.IsAvailable = input.Status == "Available" && input.IsAvailable;
        }
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
        if (!CanManage(listing)) return Forbid();
        return View(listing);
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


    private bool CanManage(Listing listing)
    {

        return listing.SellerId == _userManager.GetUserId(User) || User.IsInRole(UserRoles.Admin);
    }


    private async Task SetCategoryOptionsAsync(int? selectedId = null)
    {
        var categories = await _db.Categories.AsNoTracking().OrderBy(x => x.Name).ToListAsync();

        ViewBag.Categories = new SelectList(categories, nameof(Category.Id), nameof(Category.Name), selectedId);
    }


    private void ValidatePhotos(List<IFormFile>? photos, int currentPhotoCount)
    {

        if (photos is null || photos.Count == 0) return;

        if (currentPhotoCount + photos.Count > 5)
        {
            ModelState.AddModelError("photos", "Each listing can have up to 5 photos.");
        }

        string[] allowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        string[] allowedContentTypes = { "image/jpeg", "image/png", "image/webp" };
        foreach (var photo in photos)
        {
            string extension = Path.GetExtension(photo.FileName).ToLowerInvariant();
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

        string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");

        Directory.CreateDirectory(uploadFolder);
        foreach (var photo in photos)
        {
            string extension = Path.GetExtension(photo.FileName).ToLowerInvariant();

            string fileName = Guid.NewGuid().ToString("N") + extension;
            string filePath = Path.Combine(uploadFolder, fileName);

            await using var stream = System.IO.File.Create(filePath);
            await photo.CopyToAsync(stream);
            _db.ListingPhotos.Add(new ListingPhoto
            {
                ListingId = listingId,
                ImageUrl = "/uploads/listings/" + fileName
            });
        }
        await _db.SaveChangesAsync();
    }


    private void DeletePhotoFiles(IEnumerable<ListingPhoto> photos)
    {
        string uploadFolder = Path.Combine(_environment.WebRootPath, "uploads", "listings");
        foreach (var photo in photos)
        {

            string fileName = Path.GetFileName(photo.ImageUrl);
            string filePath = Path.Combine(uploadFolder, fileName);
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
    }
}
