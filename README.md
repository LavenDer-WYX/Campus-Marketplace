# Campus Marketplace - Week 7 Integrated Build

This folder combines the three team prototypes into one ASP.NET Core MVC application.

## Integration choices

- Yibo's ASP.NET Core Identity project is the authentication foundation.
- The home-feed branch supplies the marketplace layout, responsive item cards and client-side home filtering.
- The database branch supplies the Category, Listing and ListingPhoto concepts.
- Listings reference `AppUser.Id` through `SellerId`; the separate custom User table was intentionally not copied.

## Implemented in this build

- Student registration and login with ASP.NET Core Identity password hashing
- Automatic Student role assignment and Student/Admin role creation
- SQL Server EF Core schema and migrations
- Database-backed home feed
- Listing search and category/condition/type filtering
- Create, view, edit, delete and manage personal listings
- Profile page showing the signed-in student's details and listings
- Persistent favourites and a personal favourites page
- Buyer/seller conversations with saved messages
- Buy, swap and giveaway requests with basic trade statuses
- Listing photo upload with file type, size and count checks
- Simple Massey email-domain check during registration

## Requirements

- .NET 10 SDK
- Visual Studio 2026 when using Visual Studio
- SQL Server LocalDB (`MSSQLLocalDB`)

## Run

```powershell
dotnet restore
dotnet run
```

The application applies pending migrations and creates the Student and Admin roles during startup. The development database is `MarketPlaceWeek7Db`.

## Simple Week 7 backend notes

- Uploaded files are saved under `wwwroot/uploads/listings` and are not committed to Git.
- The campus check only checks the email domain; real email confirmation still needs an email service.
- Real-time chat is not used. Messages are saved when the user submits the form.

## Not implemented yet

- Ratings, reports and notifications
- Admin dashboard and AI feature
