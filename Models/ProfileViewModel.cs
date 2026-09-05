namespace WebApplication1.Models;

public class ProfileViewModel
{
    public required AppUser User { get; init; }
    public IReadOnlyList<Listing> Listings { get; init; } = [];
}
