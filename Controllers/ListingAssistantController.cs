
using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.RateLimiting;

using Microsoft.EntityFrameworkCore;

using WebApplication1.Data;
using WebApplication1.Models;
using WebApplication1.Services;

namespace WebApplication1.Controllers;


[Authorize]
public class ListingAssistantController : Controller
{

    private readonly ApplicationDbContext _db;

    private readonly ListingAssistantService _assistant;


    public ListingAssistantController(ApplicationDbContext db, ListingAssistantService assistant)
    {

        _db = db;

        _assistant = assistant;
    }


    [HttpPost]

    [ValidateAntiForgeryToken]

    [EnableRateLimiting("listing-assistant")]
    public async Task<IActionResult> Generate(GenerateDescriptionViewModel input, CancellationToken cancellationToken)
    {

        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Enter a title (up to 200 characters), a category and a valid condition. Age is limited to 80 characters and accessories to 300." });
        }

        string? categoryName = await _db.Categories.AsNoTracking()
            .Where(x => x.Id == input.CategoryId).Select(x => x.Name).FirstOrDefaultAsync(cancellationToken);

        if (categoryName == null)
        {
            return BadRequest(new { success = false, message = "Please choose an existing category." });
        }

        DescriptionResult result = await _assistant.GenerateDescriptionAsync(input, categoryName, cancellationToken);

        return StatusCode(result.StatusCode, new { success = result.Success, description = result.Description, message = result.Message });
    }


    [HttpPost]

    [ValidateAntiForgeryToken]

    [EnableRateLimiting("listing-assistant")]
    public async Task<IActionResult> Price(PriceSuggestionViewModel input, CancellationToken cancellationToken)
    {

        if (!ModelState.IsValid)
        {
            return BadRequest(new { success = false, message = "Enter a title, category and valid condition before requesting an AI price suggestion." });
        }

        string? categoryName = await _db.Categories.AsNoTracking()
            .Where(x => x.Id == input.CategoryId)
            .Select(x => x.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (categoryName == null)
        {
            return BadRequest(new { success = false, message = "Please choose an existing category." });
        }

        PriceSuggestionResult result = await _assistant.SuggestPriceAsync(input, categoryName, cancellationToken);
        return StatusCode(result.StatusCode, new { success = result.Success, result, message = result.Message });
    }
}
