
using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;


namespace WebApplication1.Models;



public class AdminDashboardViewModel
{

    public int UserCount { get; set; }


    public int ListingCount { get; set; }


    public int AvailableListingCount { get; set; }


    public int CompletedTradeCount { get; set; }


    public int PendingReportCount { get; set; }
}


public class ReportCreateViewModel
{

    [Range(1, int.MaxValue)]
    public int ListingId { get; set; }


    [Required, StringLength(1000)]
    public string Reason { get; set; } = string.Empty;


    [ValidateNever]
    public string ListingTitle { get; set; } = string.Empty;
}


public class AdminReportActionViewModel
{

    [Range(1, int.MaxValue)]
    public int Id { get; set; }


    [StringLength(1000)]
    public string? AdminNote { get; set; }


    [ValidateNever]
    public Report? Report { get; set; }
}
