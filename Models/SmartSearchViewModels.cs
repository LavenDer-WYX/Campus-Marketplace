using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;


public class SmartSearchInputViewModel
{

    [Required, StringLength(300, MinimumLength = 3)]
    public string Query { get; set; } = string.Empty;
}


public class SmartSearchFilters
{
    public string? Keyword { get; set; }
    public string? Category { get; set; }
    public decimal? MinimumPrice { get; set; }
    public decimal? MaximumPrice { get; set; }
    public string? Condition { get; set; }
    public string? Location { get; set; }
    public string? ListingType { get; set; }
    public int? PostedWithinDays { get; set; }
    public string SortBy { get; set; } = "newest";
}


public class SmartSearchResult
{
    public bool Success { get; set; }
    public SmartSearchFilters? Filters { get; set; }
    public string Message { get; set; } = string.Empty;
}
