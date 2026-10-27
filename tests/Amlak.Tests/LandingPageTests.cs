using Amlak.Web.Data;
using Amlak.Web.Domain;
using Amlak.Web.Pages.Properties;
using Amlak.Web.Services;
using Amlak.Web.Services.Seo;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Tests;

public class LandingPageTests : IDisposable
{
    private readonly SqliteConnection _conn = new("DataSource=:memory:");

    public LandingPageTests() => _conn.Open();
    public void Dispose() => _conn.Dispose();

    private AppDbContext Seed()
    {
        var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_conn).Options);
        db.Database.EnsureCreated();
        var now = DateTime.UtcNow;
        Property P(string slug, DealType deal, PropertyKind kind, string city, string district, int area, long price,
            bool? elevator = null, int? parking = null)
        {
            var details = ListingDetails.Of(slug, slug, kind, area, city, district, parking: parking) with { HasElevator = elevator };
            var p = Property.Create(slug, details, deal == DealType.Sale ? Pricing.ForSale(price) : Pricing.ForRent(price, null), now);
            p.Publish(now);
            return p;
        }
        db.Properties.AddRange(
            P("a", DealType.Sale, PropertyKind.Apartment, "تهران", "سعادت‌آباد", 100, 10_000_000_000, elevator: true, parking: 1),
            P("b", DealType.Sale, PropertyKind.Apartment, "تهران", "سعادت‌آباد", 200, 30_000_000_000, elevator: false),
            P("c", DealType.Sale, PropertyKind.Apartment, "تهران", "ونک", 80, 6_000_000_000),
            P("d", DealType.Rent, PropertyKind.Apartment, "تهران", "ونک", 60, 400_000_000),
            P("e", DealType.Sale, PropertyKind.Villa, "کرج", "مشکین‌دشت", 500, 28_000_000_000));
        db.SaveChanges();
        return db;
    }

    private static IndexModel Page(AppDbContext db, string path, string query = "")
    {
        var http = new DefaultHttpContext();
        http.Request.Path = path;
        http.Request.QueryString = new QueryString(query);
        var model = new IndexModel(db) { PageContext = new PageContext { HttpContext = http } };
        // Mimic model binding of the query string.
        var q = http.Request.Query;
        if (q.TryGetValue("deal", out var deal)) model.Deal = Enum.TryParse<DealType>(deal, out var d) ? d : null;
        if (q.TryGetValue("city", out var city)) model.City = city;
        if (q.TryGetValue("elevator", out var el)) model.Elevator = el == "true";
        if (q.TryGetValue("parking", out var pk)) model.Parking = pk == "true";
        return model;
    }

    [Fact]
    public void Category_paths_follow_city_category_district()
    {
        var apt = Taxonomy.For(DealType.Sale, PropertyKind.Apartment)!;
        Assert.Equal("/املاک/تهران/خرید-آپارتمان/سعادت-آباد", Taxonomy.Path("تهران", apt, "سعادت‌آباد"));
        Assert.Equal("/املاک/ایران/خرید-آپارتمان", Taxonomy.Path(null, apt, "سعادت‌آباد")); // district needs a city
        Assert.Equal("/املاک/تهران", Taxonomy.Path("تهران", null));
        Assert.Equal("/املاک", Taxonomy.Path(null, null));
        Assert.Null(Taxonomy.For(null, PropertyKind.Apartment));
        Assert.Equal(apt, Taxonomy.Find("خرید-آپارتمان"));
        Assert.Null(Taxonomy.Find("نامعتبر"));
        Assert.Equal("خرید آپارتمان در سعادت‌آباد تهران", Taxonomy.Heading(apt, "تهران", "سعادت‌آباد"));
        // Every deal × kind pair, and each deal on its own, has exactly one category with a unique slug.
        Assert.Equal(Taxonomy.All.Count, Taxonomy.All.Select(c => c.Slug).Distinct().Count());
        foreach (var deal in Enum.GetValues<DealType>())
            foreach (var kind in Enum.GetValues<PropertyKind>().Cast<PropertyKind?>().Append(null))
                Assert.NotNull(Taxonomy.For(deal, kind));
    }

    [Fact]
    public async Task Landing_page_resolves_slugs_and_filters_to_its_district()
    {
        using var db = Seed();
        var page = Page(db, "/املاک/تهران/خرید-آپارتمان/سعادت-آباد");
        var result = await page.OnGetAsync("تهران", "خرید-آپارتمان", "سعادت-آباد");

        Assert.IsType<PageResult>(result);
        Assert.Equal("سعادت‌آباد", page.District);
        Assert.Equal(DealType.Sale, page.Deal);
        Assert.Equal(PropertyKind.Apartment, page.Kind);
        Assert.Equal(["a", "b"], page.Items.Select(x => x.Slug).Order());
        Assert.Equal(2, page.Districts.Count); // سعادت‌آباد and ونک have sale apartments
        Assert.Equal(125_000_000, page.AvgPricePerM2); // (100M + 150M per m²) / 2
        Assert.False(page.UnknownPlace);
    }

    [Fact]
    public async Task Old_query_links_redirect_permanently_to_the_clean_path_keeping_other_filters()
    {
        using var db = Seed();
        var page = Page(db, "/املاک", "?deal=Sale&city=تهران&elevator=true");
        var result = Assert.IsType<RedirectResult>(await page.OnGetAsync(null, null, null));

        Assert.True(result.Permanent);
        Assert.Equal(string.Join("/", "/املاک/تهران/خرید".Split('/').Select(Uri.EscapeDataString)) + "?elevator=true", result.Url);
    }

    [Fact]
    public async Task Amenity_filters_and_unknown_category_and_place()
    {
        using var db = Seed();
        var page = Page(db, "/املاک/تهران/خرید-آپارتمان", "?elevator=true&parking=true");
        await page.OnGetAsync("تهران", "خرید-آپارتمان", null);
        Assert.Equal(["a"], page.Items.Select(x => x.Slug));

        Assert.IsType<NotFoundResult>(await Page(db, "/املاک/تهران/نامعتبر").OnGetAsync("تهران", "نامعتبر", null));

        var unknown = Page(db, "/املاک/اصفهان");
        Assert.IsType<PageResult>(await unknown.OnGetAsync("اصفهان", null, null));
        Assert.True(unknown.UnknownPlace);
        Assert.Equal(0, unknown.Total);
    }

    [Fact]
    public void Sitemap_lists_landing_pages_that_have_listings_only()
    {
        using var db = Seed();
        var paths = SeoFiles.LandingPaths(db.Properties.ToList()).Select(Uri.UnescapeDataString).ToList();

        Assert.Contains("/املاک/تهران", paths);
        Assert.Contains("/املاک/تهران/خرید-آپارتمان/سعادت-آباد", paths);
        Assert.Contains("/املاک/تهران/اجاره-آپارتمان/ونک", paths);
        Assert.Contains("/املاک/ایران/خرید-ویلا", paths);
        Assert.Contains("/املاک/کرج/خرید/مشکین-دشت", paths);
        Assert.DoesNotContain("/املاک/کرج/رهن-و-اجاره", paths);
        Assert.DoesNotContain("/املاک/تهران/اجاره-آپارتمان/سعادت-آباد", paths);
        Assert.Equal(paths.Count, paths.Distinct().Count());
    }

    [Fact]
    public void Relative_time_reads_like_listing_sites()
    {
        var now = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal("لحظاتی پیش", Fa.Ago(now.AddSeconds(-20), now));
        Assert.Equal("۳ ساعت پیش", Fa.Ago(now.AddHours(-3), now));
        Assert.Equal("دیروز", Fa.Ago(now.AddDays(-1), now));
        Assert.Equal("۲ هفته پیش", Fa.Ago(now.AddDays(-15), now));
    }
}
