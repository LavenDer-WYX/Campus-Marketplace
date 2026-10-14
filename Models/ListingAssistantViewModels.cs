
using System.ComponentModel.DataAnnotations;


namespace WebApplication1.Models;


public class GenerateDescriptionViewModel
{

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;


    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }


    [Required, RegularExpression("^(Like new|Good|Fair|Poor)$")]
    public string Condition { get; set; } = string.Empty;


    [StringLength(80)]
    public string? Age { get; set; }


    [StringLength(300)]
    public string? Accessories { get; set; }
}


public class PriceSuggestionViewModel
{

    [Required, StringLength(200)]
    public string Title { get; set; } = string.Empty;


    [Range(1, int.MaxValue)]
    public int CategoryId { get; set; }


    [Required, RegularExpression("^(Like new|Good|Fair|Poor)$")]
    public string Condition { get; set; } = string.Empty;


    [StringLength(80)]
    public string? Age { get; set; }

    [StringLength(300)]
    public string? Accessories { get; set; }
}


public class DescriptionResult
{

    public int StatusCode { get; set; }


    public bool Success { get; set; }


    public string Message { get; set; } = string.Empty;


    public string? Description { get; set; }
}


public class PriceSuggestionResult
{

    public int StatusCode { get; set; }


    public bool Success { get; set; }


    public int SampleCount { get; set; }


    public bool UsedCategoryOnly { get; set; }


    public decimal? SuggestedPrice { get; set; }
    public decimal? SuggestedMinimumPrice { get; set; }
    public decimal? SuggestedMaximumPrice { get; set; }


    public decimal? ObservedMinimumPrice { get; set; }
    public decimal? ObservedMaximumPrice { get; set; }


    public string Currency { get; set; } = "NZD";


    public string Message { get; set; } = string.Empty;
}
