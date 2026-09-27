# Campus Marketplace - Week 8 Backend Build

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
- Instant `Buy now` for sale listings; the first buyer reserves the item without seller approval
- Listing photo upload with file type, size and count checks
- Simple Massey email-domain check during registration
- Student item reports with personal report history
- Admin-only dashboard, report review and listing moderation
- In-app notifications for messages, trades and report decisions
- AI description draft integration with safe configuration and request limits
- Database-based asking-price reference for listing creation

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

See `BACKEND_GUIDE_ZH.md` for a beginner-friendly Chinese explanation of the backend files and request flows.

## Make an existing user an admin

Register the account first, stop the website, then run:

```powershell
dotnet run -- --make-admin registered-email@massey.ac.nz
```

Log out and sign in again before opening `/Admin`.

## Optional Doubao AI configuration

The listing-writing assistant uses the Volcengine Ark Chat Completions API. First create an Ark API key and enable a Doubao model. Do not commit the key. Store local development values with User Secrets:

```powershell
dotnet user-secrets set "ListingAssistant:ApiKey" "your-ark-api-key"
```

The committed configuration uses `doubao-seed-2-0-lite-260428` and the Beijing Ark endpoint. If your Ark account uses a different enabled Model ID or a custom `ep-...` endpoint ID, override it locally with `dotnet user-secrets set "ListingAssistant:Model" "your-model-or-endpoint-id"`.

Without an API key the AI description endpoint stays disabled and makes no external request. The database price reference still works.

## Backend notes

- Uploaded files are saved under `wwwroot/uploads/listings` and are not committed to Git.
- The campus check only checks the email domain; real email confirmation still needs an email service.
- Real-time chat is not used. Messages are saved when the user submits the form.

## Not implemented yet

- Real email-link verification
- Ratings and user appeals workflow
- Real-time chat and push/email notifications
- Admin account-management UI and audit log
