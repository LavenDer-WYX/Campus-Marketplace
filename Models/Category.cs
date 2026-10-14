using System.ComponentModel.DataAnnotations;

namespace WebApplication1.Models;


public class Category
{

    public int Id { get; set; }


    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;


    [StringLength(500)]
    public string? Description { get; set; }


    public ICollection<Listing> Listings { get; set; } = new List<Listing>();
}
