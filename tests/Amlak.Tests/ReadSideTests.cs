using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Read;
using Microsoft.Data.Sqlite;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Amlak.Tests;

/// <summary>
/// Read side (views + ListingQueries). Every test runs twice: on SQLite (always) and on a real
/// SQL Server through the migrations (when AMLAK_TEST_SQLSERVER holds a connection string without a
/// database name, e.g. "Server=localhost,1433;User Id=sa;Password=...;TrustServerCertificate=True").
/// </summary>
public abstract class ReadSideTests : IAsyncLifetime
{
    /// <summary>Null when the database is not available here; the tests then report as skipped.</summary>
    protected abstract Task<AppDbContext?> CreateDbAsync();
    public virtual Task DisposeAsync() => Task.CompletedTask;

    private AppDbContext? _db;
    private AppDbContext Db => _db ?? throw new SkipException("AMLAK_TEST_SQLSERVER is not set");
    private ListingQueries Listings => new(Db);
    private static readonly DateTime Now = new(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);

    // Built through the aggregate (Create/Publish/AddPhoto) the way the admin panel does.
    private static Property Make(string slug, PropertyKind kind, string city, string district, int area, Pricing pricing,
        int? beds = null, int ageDays = 1, ListingStatus status = ListingStatus.Published,
        Func<ListingDetails, ListingDetails>? extra = null)
    {
        var details = ListingDetails.Of(slug, "", kind, area, city, district, bedrooms: beds);
        if (extra is not null) details = extra(details);
        var p = Property.Create(slug, details, pricing, Now.AddDays(-ageDays));
        if (status != ListingStatus.Draft) p.ChangeStatus(status, Now.AddDays(-ageDays));
        return p;
    }

    public async Task InitializeAsync()
    {
        _db = await CreateDbAsync();
        if (_db is null) return;

        var vanak = Make("vanak-sale", PropertyKind.Apartment, "تهران", "ونک", 100, Pricing.ForSale(10_000_000_000), beds: 2, ageDays: 1,
            extra: d => d with { HasElevator = true, Parking = 1, YearBuilt = 1400 });
        var second = vanak.AddPhoto("/u/second.jpg", Now);
        var cover = vanak.AddPhoto("/u/cover.jpg", Now);
        (second.SortOrder, cover.SortOrder) = (2, 0);
        (cover.Alt, cover.Width, cover.Height, cover.Variants) = ("کاور", 1600, 1200, "480,960");
        var rent = Make("vanak-rent", PropertyKind.Apartment, "تهران", "ونک", 60, Pricing.ForRent(400_000_000, 8_000_000, convertible: true), beds: 1, ageDays: 2);
        var villa = Make("karaj-villa", PropertyKind.Villa, "کرج", "مشکین‌دشت", 500, Pricing.ForSale(28_000_000_000), beds: 4, ageDays: 3,
            extra: d => d with { HasStorage = true, YearBuilt = 1390 });
        villa.AddPhoto("/u/villa.jpg", Now);
        var saadat = Make("saadat-sale", PropertyKind.Apartment, "تهران", "سعادت‌آباد", 120, Pricing.ForSale(12_000_000_000), beds: 3, ageDays: 4);
        var draft = Make("draft", PropertyKind.Apartment, "تهران", "ونک", 90, Pricing.ForSale(1), status: ListingStatus.Draft);
        var closed = Make("closed", PropertyKind.Office, "تهران", "ونک", 90, Pricing.ForSale(1), status: ListingStatus.Closed);
        var pending = Make("pending", PropertyKind.Office, "تهران", "ونک", 90, Pricing.ForSale(1), status: ListingStatus.Pending);
        _db.Properties.AddRange(vanak, rent, villa, saadat, draft, closed, pending);
        await _db.SaveChangesAsync();

        var handled = Lead.Submit("c", PhoneNumber.Parse("09120000003"), null, null, Now);
        handled.ToggleHandled();
        _db.Leads.AddRange(Lead.Submit("a", PhoneNumber.Parse("09120000001"), null, null, Now),
            Lead.Submit("b", PhoneNumber.Parse("09120000002"), null, vanak.Id, Now), handled);
        Ticket T(string code, TicketStatus status)
        {
            var t = Ticket.Open(code, "a", PhoneNumber.Parse("09120000001"), "s", TicketCategory.General, "m", Now);
            t.ChangeStatus(status, Now);
            return t;
        }
        _db.Tickets.AddRange(T("T1", TicketStatus.Open), T("T2", TicketStatus.Answered), T("T3", TicketStatus.Answered), T("T4", TicketStatus.Closed));
        _db.ChannelPosts.AddRange(new ChannelPost { PropertyId = vanak.Id, Channel = Channel.Telegram, Error = "timeout" },
            new ChannelPost { PropertyId = vanak.Id, Channel = Channel.Bale, ExternalMessageId = "5" });
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();
    }

    private Task<List<string>> Slugs(ListingFilter f) =>
        Listings.SearchAsync(f, 1, 50).ContinueWith(t => t.Result.Items.Select(c => c.Slug).ToList());

    [SkippableFact]
    public async Task Place_prices_weight_sale_price_by_area_and_average_deposits()
    {
        var tehran = await Listings.PlacePricesAsync("تهران");
        var vanak = Assert.Single(tehran, r => r.Name == "ونک");
        Assert.Equal((1, 100_000_000L, 1, 400_000_000L), (vanak.SaleListings, vanak.SalePricePerM2!.Value, vanak.RentListings, vanak.AvgDeposit!.Value));
        var saadat = Assert.Single(tehran, r => r.Name == "سعادت‌آباد");
        Assert.Null(saadat.AvgDeposit);
        Assert.Equal("ونک", tehran[0].Name); // busiest first

        var cities = await Listings.PlacePricesAsync(null);
        Assert.Equal(100_000_000, cities.Single(r => r.Name == "تهران").SalePricePerM2); // 22 billion / 220 m²
        Assert.Equal(56_000_000, cities.Single(r => r.Name == "کرج").SalePricePerM2);
    }

    [SkippableFact]
    public async Task Cards_hold_only_published_listings_with_the_first_photo_as_cover()
    {
        var cards = await Db.ListingCards.ToListAsync();
        Assert.Equal(["karaj-villa", "saadat-sale", "vanak-rent", "vanak-sale"], cards.Select(c => c.Slug).Order(StringComparer.Ordinal));

        var vanak = cards.Single(c => c.Slug == "vanak-sale");
        Assert.Equal("/u/cover.jpg", vanak.CoverUrl);
        Assert.Equal("کاور", vanak.CoverAlt);
        Assert.Equal(2, vanak.PhotoCount);
        Assert.Equal((1600, 1200, "480,960"), (vanak.Cover!.Width, vanak.Cover.Height, vanak.Cover.Variants)); // for the srcset
        Assert.True(vanak.HasElevator);
        Assert.Equal(10_000_000_000, vanak.SortPrice);

        var rent = cards.Single(c => c.Slug == "vanak-rent");
        Assert.Null(rent.CoverUrl);
        Assert.Equal(0, rent.PhotoCount);
        Assert.Equal(400_000_000, rent.SortPrice); // deposit stands in for price on rentals
        Assert.True(rent.Convertible);
    }

    [SkippableFact]
    public async Task Search_applies_each_filter()
    {
        Assert.Equal(["vanak-sale", "karaj-villa", "saadat-sale"], await Slugs(new() { Deal = DealType.Sale }));
        Assert.Equal(["karaj-villa"], await Slugs(new() { Kind = PropertyKind.Villa }));
        Assert.Equal(["vanak-sale", "vanak-rent"], await Slugs(new() { City = " تهران ", District = "ونک" }));
        Assert.Equal(["vanak-sale", "vanak-rent"], await Slugs(new() { MaxPrice = 10_000_000_000 }));
        Assert.Equal(["karaj-villa", "saadat-sale"], await Slugs(new() { MinPrice = 11_000_000_000 }));
        Assert.Equal(["vanak-rent"], await Slugs(new() { Deal = DealType.Rent, MaxRent = 10_000_000 }));
        Assert.Empty(await Slugs(new() { Deal = DealType.Rent, MaxRent = 1_000_000 }));
        Assert.Equal(["vanak-sale", "saadat-sale"], await Slugs(new() { MinArea = 100, MaxArea = 200 }));
        Assert.Equal(["karaj-villa", "saadat-sale"], await Slugs(new() { MinBeds = 3 }));
        Assert.Equal(["vanak-sale"], await Slugs(new() { MinYearBuilt = 1395 }));
        Assert.Equal(["vanak-sale"], await Slugs(new() { Elevator = true, Parking = true }));
        Assert.Equal(["karaj-villa"], await Slugs(new() { Storage = true }));
        Assert.Equal(["vanak-sale", "karaj-villa"], await Slugs(new() { HasPhoto = true }));
        Assert.Equal(["vanak-rent"], await Slugs(new() { Convertible = true }));
        Assert.Empty(await Slugs(new() { City = "اصفهان" }));
        Assert.Equal(["vanak-sale", "vanak-rent", "saadat-sale"], await Slugs(new() { City = "تهران", Areas = ["ونک", "سعادت‌آباد"] }));
        Assert.Equal(["saadat-sale"], await Slugs(new() { Areas = ["سعادت‌آباد", "پونک"] }));
        Assert.Equal(["karaj-villa"], await Slugs(new() { Text = " کرج " })); // city
        Assert.Equal(["vanak-sale", "vanak-rent"], await Slugs(new() { Text = "ونک" })); // district and title
    }

    [SkippableFact]
    public async Task Search_sorts_and_clamps_the_page()
    {
        Assert.Equal(["vanak-rent", "vanak-sale", "saadat-sale", "karaj-villa"], await Slugs(new() { Sort = ListingSort.Cheapest }));
        Assert.Equal(["karaj-villa", "saadat-sale", "vanak-sale", "vanak-rent"], await Slugs(new() { Sort = ListingSort.Priciest }));
        Assert.Equal("karaj-villa", (await Slugs(new() { Sort = ListingSort.Largest }))[0]);

        var page = await Listings.SearchAsync(new(), 99, 3);
        Assert.Equal(4, page.Total);
        Assert.Equal(2, page.Pages);
        Assert.Equal(2, page.Page);
        Assert.Equal(["saadat-sale"], page.Items.Select(c => c.Slug));

        var empty = await Listings.SearchAsync(new() { City = "اصفهان" }, 5, 3);
        Assert.Equal((0, 1), (empty.Total, empty.Page));
    }

    [SkippableFact]
    public async Task Facets_count_published_listings_per_kind_and_city()
    {
        var kinds = await Listings.CountByKindAsync();
        Assert.Equal(3, kinds[PropertyKind.Apartment]);
        Assert.Equal(1, kinds[PropertyKind.Villa]);
        Assert.False(kinds.ContainsKey(PropertyKind.Office)); // only the closed one

        Assert.Equal(2, (await Listings.CountByKindAsync(DealType.Sale))[PropertyKind.Apartment]);

        var cities = await Listings.CitiesAsync(DealType.Sale);
        Assert.Equal(["تهران", "کرج"], cities.Select(c => c.City));
        Assert.Equal(new CityFacet("تهران", 2, 11_000_000_000, 110), cities[0]);

        var areas = await Listings.TopAreasAsync(2);
        Assert.Equal(("تهران", "ونک", 2), areas[0]);
        Assert.Equal(2, areas.Count);
    }

    [SkippableFact]
    public async Task Views_follow_writes_on_the_write_model()
    {
        // Publish the draft and unpublish a live one through the aggregate, as the admin panel does.
        var draft = await Db.Properties.SingleAsync(p => p.Slug == "draft");
        draft.Publish(Now);
        var villa = await Db.Properties.SingleAsync(p => p.Slug == "karaj-villa");
        villa.Unpublish(Now);
        await Db.SaveChangesAsync();

        Assert.Equal("draft", (await Listings.LatestAsync(1)).Single().Slug);
        Assert.False((await Listings.CountByKindAsync()).ContainsKey(PropertyKind.Villa));
        Assert.Equal(4, (await Listings.CountByKindAsync())[PropertyKind.Apartment]);
        Assert.Equal(4, (await new AdminQueries(Db).CountersAsync()).Published);
    }

    [SkippableFact]
    public async Task Admin_counters_come_back_in_one_row()
    {
        var c = await new AdminQueries(Db).CountersAsync();
        Assert.Equal((4, 1, 1, 2, 1, 2, 1), (c.Published, c.Drafts, c.PendingListings, c.NewLeads, c.OpenTickets, c.AnsweredTickets, c.ChannelErrors));
    }

    [SkippableFact]
    public async Task Saved_and_similar_lists_skip_unpublished_and_self()
    {
        var ids = await Db.Properties.Select(p => p.Id).ToListAsync();
        Assert.Equal(4, (await Listings.ByIdsAsync(ids)).Count);

        var vanak = await Listings.DetailAsync("vanak-sale");
        Assert.NotNull(vanak);
        Assert.Equal(["/u/cover.jpg", "/u/second.jpg"], vanak.Photos.Select(p => p.Url));
        Assert.Null(await Listings.DetailAsync("draft"));

        Assert.Equal(["saadat-sale"], (await Listings.SimilarAsync(vanak, 3)).Select(c => c.Slug));
        // Not enough of the same kind: the rest are filled with other kinds, newest first.
        Assert.Equal(["vanak-sale", "saadat-sale"],
            (await Listings.SimilarAsync(-1, DealType.Sale, PropertyKind.Villa, "تهران", 3)).Select(c => c.Slug));
    }
}

public sealed class SqliteReadSideTests : ReadSideTests
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    protected override async Task<AppDbContext?> CreateDbAsync()
    {
        await _conn.OpenAsync();
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        await db.Database.EnsureCreatedAsync();
        await ReadViews.CreateSqliteAsync(db);
        return db;
    }

    public override async Task DisposeAsync() => await _conn.DisposeAsync();
}

public sealed class SqlServerReadSideTests : ReadSideTests
{
    public static readonly string? Server = Environment.GetEnvironmentVariable("AMLAK_TEST_SQLSERVER");
    private AppDbContext? _db;

    protected override async Task<AppDbContext?> CreateDbAsync()
    {
        if (string.IsNullOrWhiteSpace(Server)) return null;
        var cs = new SqlConnectionStringBuilder(Server) { InitialCatalog = $"amlak_test_{Guid.NewGuid():N}" }.ConnectionString;
        // Identity key lengths come from the app's AddDefaultIdentity options, which a bare context lacks;
        // that shows up as a pending-change warning unrelated to these tests.
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(cs)
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning)).Options);
        await _db.Database.MigrateAsync(); // the real migrations, views and indexed view included
        return _db;
    }

    public override async Task DisposeAsync()
    {
        if (_db is null) return;
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }
}
