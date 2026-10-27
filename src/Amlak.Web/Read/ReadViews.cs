using Amlak.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Read;

/// <summary>
/// SQLite versions of the read views, for tests and local SQLite runs. EnsureCreated does not create
/// views, so call <see cref="CreateSqliteAsync"/> after it. The SQL Server versions live in the
/// ReadViews migration; keep the column lists of both in step with <see cref="ListingCard"/>.
/// </summary>
public static class ReadViews
{
    public const string SqliteListingCards = """
        CREATE VIEW IF NOT EXISTS vw_ListingCards AS
        SELECT p.Id, p.Slug, p.Title, p.Deal, p.Kind, p.City, p.District, p.AreaM2, p.Bedrooms, p.Floor, p.Parking,
               p.YearBuilt, p.HasElevator, p.HasStorage, p.HasBalcony, p.Deed, p.Convertible,
               p.Price, p.Deposit, p.MonthlyRent, p.PriceDisplay, p.SortPrice, p.PublishedAtUtc,
               (SELECT f.Url FROM PropertyPhotos f WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id LIMIT 1) AS CoverUrl,
               (SELECT f.Alt FROM PropertyPhotos f WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id LIMIT 1) AS CoverAlt,
               (SELECT f.Width FROM PropertyPhotos f WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id LIMIT 1) AS CoverWidth,
               (SELECT f.Height FROM PropertyPhotos f WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id LIMIT 1) AS CoverHeight,
               (SELECT f.Variants FROM PropertyPhotos f WHERE f.PropertyId = p.Id ORDER BY f.SortOrder, f.Id LIMIT 1) AS CoverVariants,
               (SELECT COUNT(*) FROM PropertyPhotos f WHERE f.PropertyId = p.Id) AS PhotoCount
        FROM Properties p
        WHERE p.Status = 1;
        """;

    public const string SqliteListingFacets = """
        CREATE VIEW IF NOT EXISTS vw_ListingFacets AS
        SELECT Deal, Kind, City, District, COUNT(*) AS Listings,
               SUM(SortPrice) AS SumPrice, SUM(AreaM2) AS SumArea
        FROM Properties
        WHERE Status = 1
        GROUP BY Deal, Kind, City, District;
        """;

    /// <summary>Plain ANSI SQL; the migration uses the same text on SQL Server.</summary>
    public const string AdminCounters = """
        CREATE VIEW vw_AdminCounters AS
        SELECT
            (SELECT COUNT(*) FROM Properties WHERE Status = 1) AS Published,
            (SELECT COUNT(*) FROM Properties WHERE Status = 0) AS Drafts,
            (SELECT COUNT(*) FROM Properties WHERE Status = 3) AS PendingListings,
            (SELECT COUNT(*) FROM Leads WHERE Handled = 0) AS NewLeads,
            (SELECT COUNT(*) FROM Tickets WHERE Status = 0) AS OpenTickets,
            (SELECT COUNT(*) FROM Tickets WHERE Status = 1) AS AnsweredTickets,
            (SELECT COUNT(*) FROM ChannelPosts WHERE Error IS NOT NULL) AS ChannelErrors;
        """;

    public static async Task CreateSqliteAsync(AppDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync(SqliteListingCards);
        await db.Database.ExecuteSqlRawAsync(SqliteListingFacets);
        await db.Database.ExecuteSqlRawAsync(AdminCounters.Replace("CREATE VIEW", "CREATE VIEW IF NOT EXISTS"));
    }
}
