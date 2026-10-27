using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Pages.Properties;
using Amlak.Web.Read;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Tests;

/// <summary>
/// Pins what the public read pages return (filters, sort, paging, cover photo, similar listings),
/// so moving these reads to another query shape (views, read models) can be checked against it.
/// </summary>
public sealed class ListingQueryTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");
    private readonly DateTime _now = DateTime.UtcNow;

    public ListingQueryTests()
    {
        _conn.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
        ReadViews.CreateSqliteAsync(db).GetAwaiter().GetResult();
        db.Properties.AddRange(
            Make("sale-tehran-new", DealType.Sale, PropertyKind.Apartment, "تهران", 120, 3, price: 12_000_000_000, ageDays: 1, photos: 3),
            Make("sale-tehran-old", DealType.Sale, PropertyKind.Apartment, "تهران", 80, 2, price: 6_000_000_000, ageDays: 5, photos: 1),
            Make("sale-tehran-villa", DealType.Sale, PropertyKind.Villa, "تهران", 400, 4, price: 30_000_000_000, ageDays: 3),
            Make("sale-karaj", DealType.Sale, PropertyKind.Apartment, "کرج", 100, 2, price: 4_000_000_000, ageDays: 2),
            Make("rent-tehran", DealType.Rent, PropertyKind.Apartment, "تهران", 60, 1, deposit: 400_000_000, ageDays: 4),
            Make("rent-tehran-shop", DealType.Rent, PropertyKind.Commercial, "تهران", 50, null, deposit: 900_000_000, ageDays: 6),
            Make("draft-tehran", DealType.Sale, PropertyKind.Apartment, "تهران", 90, 2, price: 1, ageDays: 0, status: ListingStatus.Draft));
        db.SaveChanges();
    }

    public void Dispose() => _conn.Dispose();

    private AppDbContext NewDb() => new(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);

    private Property Make(string slug, DealType deal, PropertyKind kind, string city, int area, int? beds,
        long? price = null, long? deposit = null, int ageDays = 0, int photos = 0, ListingStatus status = ListingStatus.Published)
    {
        var at = _now.AddDays(-ageDays);
        var pricing = deal == DealType.Sale ? Pricing.ForSale(price) : Pricing.ForRent(deposit, 1_000_000);
        var p = Property.Create(slug, ListingDetails.Of(slug, "d", kind, area, city, "مرکز", bedrooms: beds), pricing, at);
        for (var i = 0; i < photos; i++) p.AddPhoto($"/u/{slug}-{i}.jpg", at);
        if (status == ListingStatus.Published) p.Publish(at);
        p.ClearDomainEvents();
        return p;
    }

    private async Task<List<string>> Query(Action<IndexModel> set)
    {
        await using var db = NewDb();
        var m = new IndexModel(new ListingQueries(db));
        set(m);
        await Run(m);
        return m.Items.Select(x => x.Slug).ToList();
    }

    /// <summary>Runs the list page as if requested at its own canonical path, so it renders instead of redirecting.</summary>
    private static async Task Run(IndexModel m)
    {
        m.City = m.City?.Trim();
        m.PageContext = new PageContext { HttpContext = new DefaultHttpContext { Request = { Path = m.BasePath } } };
        Assert.IsType<PageResult>(await m.OnGetAsync(null, null, null));
    }

    [Fact]
    public async Task List_shows_only_published_newest_first()
    {
        var slugs = await Query(_ => { });
        Assert.Equal(["sale-tehran-new", "sale-karaj", "sale-tehran-villa", "rent-tehran", "sale-tehran-old", "rent-tehran-shop"], slugs);
    }

    [Fact]
    public async Task List_filters_by_deal_kind_city_area_and_beds()
    {
        Assert.Equal(["sale-tehran-new", "sale-tehran-villa", "sale-tehran-old"],
            await Query(m => { m.Deal = DealType.Sale; m.City = " تهران "; }));
        Assert.Equal(["rent-tehran-shop"], await Query(m => m.Kind = PropertyKind.Commercial));
        Assert.Equal(["sale-tehran-new", "sale-tehran-villa"], await Query(m => { m.Deal = DealType.Sale; m.MinArea = "۱۱۰"; }));
        Assert.Equal(["sale-tehran-new", "sale-tehran-villa"], await Query(m => m.Beds = 3));
    }

    [Fact]
    public async Task Max_price_reads_persian_digits_and_uses_deposit_for_rentals()
    {
        // ۶٬۰۰۰٬۰۰۰٬۰۰۰ with Persian digits and separators.
        Assert.Equal(["sale-karaj", "sale-tehran-old"], await Query(m => { m.Deal = DealType.Sale; m.MaxPrice = "۶٬۰۰۰٬۰۰۰٬۰۰۰"; }));
        Assert.Equal(["rent-tehran"], await Query(m => { m.Deal = DealType.Rent; m.MaxPrice = "500000000"; }));
    }

    [Fact]
    public async Task List_sorts_by_price_and_area()
    {
        Assert.Equal(["sale-karaj", "sale-tehran-old", "sale-tehran-new", "sale-tehran-villa"],
            await Query(m => { m.Deal = DealType.Sale; m.Sort = SortOrder.Cheapest; }));
        Assert.Equal("sale-tehran-villa", (await Query(m => m.Sort = SortOrder.Priciest))[0]);
        Assert.Equal("sale-tehran-villa", (await Query(m => m.Sort = SortOrder.Largest))[0]);
    }

    [Fact]
    public async Task List_counts_all_matches_and_clamps_the_page()
    {
        await using var db = NewDb();
        var m = new IndexModel(new ListingQueries(db)) { Deal = DealType.Sale, P = 99 };
        await Run(m);
        Assert.Equal(4, m.Total);
        Assert.Equal(1, m.Pages);
        Assert.Equal(1, m.P);
        Assert.Equal(4, m.Items.Count);
    }

    [Fact]
    public async Task Cards_carry_only_the_cover_photo()
    {
        await using var db = NewDb();
        var m = new IndexModel(new ListingQueries(db));
        await Run(m);
        var card = m.Items.Single(x => x.Slug == "sale-tehran-new");
        Assert.Equal("/u/sale-tehran-new-0.jpg", card.CoverUrl);
        Assert.Equal(3, card.PhotoCount);
        Assert.Null(m.Items.Single(x => x.Slug == "sale-karaj").CoverUrl);
    }

    [Fact]
    public async Task Detail_loads_every_photo_in_order_and_hides_drafts()
    {
        await using var db = NewDb();
        var m = new DetailsModel(new ListingQueries(db));
        Assert.IsType<PageResult>(await m.OnGetAsync("sale-tehran-new"));
        Assert.Equal([0, 1, 2], m.Item.Photos.Select(x => x.SortOrder));

        Assert.IsType<NotFoundResult>(await new DetailsModel(new ListingQueries(db)).OnGetAsync("draft-tehran"));
        Assert.IsType<NotFoundResult>(await new DetailsModel(new ListingQueries(db)).OnGetAsync("missing"));
    }

    [Fact]
    public async Task Similar_is_same_deal_and_city_same_kind_first_at_most_three()
    {
        await using var db = NewDb();
        var m = new DetailsModel(new ListingQueries(db));
        await m.OnGetAsync("sale-tehran-old");
        // Other Tehran sales: apartment first, then villa; never itself, Karaj, rentals or drafts.
        Assert.Equal(["sale-tehran-new", "sale-tehran-villa"], m.Similar.Select(x => x.Slug));
        Assert.Equal("/u/sale-tehran-new-0.jpg", m.Similar[0].CoverUrl); // cards carry the cover only
    }

    [Fact]
    public async Task Home_shows_latest_and_counts_published_by_kind()
    {
        await using var db = NewDb();
        var m = new Amlak.Web.Pages.IndexModel(new ListingQueries(db));
        await m.OnGetAsync();
        Assert.Equal(6, m.Latest.Count);
        Assert.Equal("sale-tehran-new", m.Latest[0].Slug);
        Assert.Equal(4, m.CountByKind[PropertyKind.Apartment]); // the draft apartment is not counted
        Assert.Equal(1, m.CountByKind[PropertyKind.Villa]);
        Assert.Equal(1, m.CountByKind[PropertyKind.Commercial]);
    }

    [Fact]
    public async Task Admin_listings_are_paged_newest_first_and_include_drafts()
    {
        await using (var seed = NewDb())
        {
            for (var i = 0; i < Amlak.Web.Pages.Admin.ListingsModel.PageSize; i++)
                seed.Properties.Add(Make($"bulk-{i}", DealType.Sale, PropertyKind.Land, "قم", 200, null, price: 1_000, ageDays: 30 + i));
            await seed.SaveChangesAsync();
        }
        await using var db = NewDb();
        Amlak.Web.Pages.Admin.ListingsModel Model(int p) => new(db, TimeProvider.System) { P = p };

        var first = Model(1);
        await first.OnGetAsync();
        Assert.Equal(57, first.Total); // 7 fixtures (draft included) + 50 bulk
        Assert.Equal(2, first.Pages);
        Assert.Equal(50, first.Items.Count);
        Assert.Equal("draft-tehran", first.Items[0].Slug);
        var withPhotos = first.Items.Single(x => x.Slug == "sale-tehran-new").Id;
        Assert.Equal("/u/sale-tehran-new-0.jpg", first.Covers[withPhotos]);
        Assert.False(first.Covers.ContainsKey(first.Items.Single(x => x.Slug == "sale-karaj").Id));

        var last = Model(9);
        await last.OnGetAsync();
        Assert.Equal(2, last.P);
        Assert.Equal(7, last.Items.Count);
        Assert.Equal("bulk-49", last.Items[^1].Slug);
        Assert.Empty(first.Items.Select(x => x.Id).Intersect(last.Items.Select(x => x.Id)));
    }
}
