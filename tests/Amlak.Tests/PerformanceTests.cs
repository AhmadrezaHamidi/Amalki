using System.Diagnostics.Metrics;
using Amlak.Domain;
using Amlak.Web.Read;
using Amlak.Web.Services;
using Amlak.Web.Services.Seo;
using Microsoft.Extensions.DependencyInjection;

namespace Amlak.Tests;

public class PerformanceTests
{
    private static ListingFacet F(string city, string district, DealType deal, long n) =>
        new() { City = city, District = district, Deal = deal, Kind = PropertyKind.Apartment, Listings = n };

    [Fact]
    public void Suggest_matches_cities_and_districts_with_counts_prefix_first()
    {
        var places = new[]
        {
            F("تهران", "سعادت‌آباد", DealType.Sale, 5), F("تهران", "سعادت‌آباد", DealType.Rent, 9),
            F("تهران", "ونک", DealType.Rent, 3), F("کرج", "سعادت", DealType.Sale, 1),
        };
        var items = Suggest.Find(places, Suggest.Normalize("سعادت"));
        Assert.Equal(["سعادت‌آباد", "سعادت"], items.Select(i => i.Label));
        Assert.Equal(14, items[0].Count);
        // Busiest deal there is rent, so the district link opens the rent page.
        Assert.Equal(Taxonomy.EncodedPath("تهران", Taxonomy.Find("رهن-و-اجاره"), "سعادت‌آباد"), items[0].Url);
        // Arabic kaf/yeh and spaces typed on some keyboards still match.
        Assert.Single(Suggest.Find(places, Suggest.Normalize("كرج")));
        Assert.Empty(Suggest.Find(places, Suggest.Normalize("اصفهان")));
    }

    [Fact]
    public void Web_vitals_keep_p75_per_page_type_and_drop_bad_reports()
    {
        using var sp = new ServiceCollection().AddMetrics().BuildServiceProvider();
        var vitals = new WebVitals(sp.GetRequiredService<IMeterFactory>(), TimeProvider.System);
        foreach (var lcp in new[] { 1000.0, 2000, 3000, 9000 })
            vitals.Record(new WebVitals.Report("/املاک/تهران", lcp, null, 0.01, null, 300));
        vitals.Record(new WebVitals.Report("/", -5, double.NaN, 99, null, null)); // nonsense: ignored
        vitals.Record(new WebVitals.Report("/Admin/Leads", 500, null, null, null, null)); // admin: ignored

        var s = vitals.Summarize();
        var lcpList = Assert.Single(s, x => x.Metric == "lcp");
        Assert.Equal("list", lcpList.Page);
        Assert.Equal(3000, lcpList.P75);
        Assert.Equal("ni", lcpList.Rating);
        Assert.Equal("good", Assert.Single(s, x => x.Metric == "cls").Rating);
        Assert.DoesNotContain(s, x => x.Page == "home");
    }

    [Theory]
    [InlineData("/", "home")]
    [InlineData("/املاک/تهران/خرید", "list")]
    [InlineData("/ملک/آپارتمان-نوساز", "detail")]
    [InlineData("/تماس", "other")]
    public void Web_vitals_group_by_page_type(string path, string type) => Assert.Equal(type, WebVitals.PageType(path));

    [Fact]
    public void Sitemap_from_selected_columns_matches_sitemap_from_listings()
    {
        var p = Property.Create("villa-1", ListingDetails.Of("ویلا", "توضیح", PropertyKind.Villa, 300, "کرج", "مهرشهر", 3),
            Pricing.For(DealType.Sale, 1_000_000, null, null), DateTime.UtcNow);
        p.AddPhoto("/uploads/b.jpg", DateTime.UtcNow);
        p.AddPhoto("/uploads/a.jpg", DateTime.UtcNow);
        p.Publish(DateTime.UtcNow);
        Assert.Equal(SeoFiles.Sitemap([p], "https://x.ir"), SeoFiles.Sitemap([SitemapEntry.Of(p)], "https://x.ir"));
    }
}
