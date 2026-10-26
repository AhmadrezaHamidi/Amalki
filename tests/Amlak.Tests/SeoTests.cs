using System.Xml.Linq;
using Amlak.Web.Domain;
using Amlak.Web.Services;
using Amlak.Web.Services.Seo;

namespace Amlak.Tests;

public class SeoTests
{
    private const string Base = "https://example.ir";

    private static Property Sample(string slug = "آپارتمان-تست") => new()
    {
        Title = "آپارتمان تست", Slug = slug, Deal = DealType.Sale, Kind = PropertyKind.Apartment, City = "تهران", District = "ونک", AreaM2 = 90,
        Price = 5_000_000_000, Status = ListingStatus.Published, PublishedAtUtc = new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc),
        Photos = { new PropertyPhoto { Url = "/uploads/a.jpg", SortOrder = 0 } },
    };

    [Fact]
    public void Sitemap_is_valid_xml_with_encoded_absolute_urls_lastmod_and_images()
    {
        var xml = SeoFiles.Sitemap([Sample()], Base);
        var doc = XDocument.Parse(xml.Substring(xml.IndexOf("<urlset", StringComparison.Ordinal)));
        var locs = doc.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();

        Assert.Contains(Base + "/", locs);
        Assert.Contains(locs, l => l.StartsWith(Base + "/%D9%85%D9%84%DA%A9/")); // /ملک/ percent-encoded
        Assert.Contains(Base + "/uploads/a.jpg", locs);
        Assert.All(locs, l => Assert.StartsWith(Base, l));
        Assert.Equal("2026-10-20", doc.Descendants().First(e => e.Name.LocalName == "lastmod").Value);
    }

    [Fact]
    public void Sitemap_escapes_xml_special_characters_in_slugs()
    {
        var xml = SeoFiles.Sitemap([Sample("a&b")], Base);
        XDocument.Parse(xml.Substring(xml.IndexOf("<urlset", StringComparison.Ordinal))); // would throw if not escaped
        Assert.Contains("a%26b", xml);
    }

    [Fact]
    public void Robots_blocks_admin_for_everyone_and_lists_sitemap_and_ai_bots()
    {
        var txt = SeoFiles.Robots(Base);
        Assert.Contains("Disallow: /Admin", txt);
        Assert.Contains("Disallow: /Account", txt);
        Assert.Contains($"Sitemap: {Base}/sitemap.xml", txt);
        Assert.Contains("User-agent: GPTBot", txt);
        Assert.Contains("User-agent: ClaudeBot", txt);
        // Each AI group must repeat the Disallow lines (a specific group replaces the * group).
        Assert.Equal(2, txt.Split("Disallow: /Admin").Length - 1);
    }

    [Fact]
    public void Llms_lists_pages_and_listings_with_price()
    {
        var txt = SeoFiles.Llms("آشیانه", Base, [Sample()]);
        Assert.StartsWith("# آشیانه", txt);
        Assert.Contains("[آپارتمان تست](" + Base + "/%D9%85%D9%84%DA%A9/", txt);
        Assert.Contains("۵ میلیارد", txt);
    }

    [Fact]
    public void JsonLd_cannot_close_the_script_tag()
    {
        var json = JsonLd.Serialize(new Dictionary<string, string> { ["name"] = "x</script><script>alert(1)</script>" });
        Assert.DoesNotContain("</script", json);
    }

    [Fact]
    public void Breadcrumbs_are_numbered_from_one()
    {
        var json = JsonLd.Serialize(JsonLd.Breadcrumbs(("خانه", Base + "/"), ("لیست", Base + "/x")));
        Assert.Contains("\"position\":1", json);
        Assert.Contains("\"position\":2", json);
        Assert.Contains("BreadcrumbList", json);
    }

    [Fact]
    public void Listing_jsonld_uses_iso_currency_absolute_images_and_no_nulls()
    {
        var p = Sample();
        p.Bedrooms = 2;
        var json = JsonLd.Serialize(JsonLd.Listing(p, Base));

        Assert.Contains("\"@type\":\"RealEstateListing\"", json);
        Assert.Contains("\"priceCurrency\":\"IRR\"", json);
        Assert.Contains("\"price\":50000000000", json); // 5 billion Toman = 50 billion Rial
        Assert.Contains("\"image\":[\"" + Base + "/uploads/a.jpg\"]", json);
        Assert.Contains("\"datePosted\":\"2026-10-20\"", json);
        Assert.Contains("\"numberOfBedrooms\":2", json);
        Assert.DoesNotContain("null", json);
        Assert.DoesNotContain("IRT", json);
    }

    [Fact]
    public void Rent_listing_has_monthly_rent_and_deposit_specs()
    {
        var p = Sample();
        p.Deal = DealType.Rent; p.Price = null; p.Deposit = 300_000_000; p.MonthlyRent = 20_000_000;
        var json = JsonLd.Serialize(JsonLd.Listing(p, Base));

        Assert.Contains("LeaseOut", json);
        Assert.Contains("\"unitCode\":\"MON\"", json);
        Assert.Contains("\"price\":200000000", json);
        Assert.Contains("\"price\":3000000000", json);
    }

    [Fact]
    public void ItemList_positions_continue_across_pages()
    {
        var json = JsonLd.Serialize(JsonLd.ItemList([Sample("a"), Sample("b")], Base, 13));
        Assert.Contains("\"position\":13", json);
        Assert.Contains("\"position\":14", json);
        Assert.Contains(Base + "/%D9%85%D9%84%DA%A9/b", json);
    }

    [Fact]
    public void Summary_states_kind_size_place_and_price()
    {
        var p = Sample();
        p.Bedrooms = 2; p.Floor = 3;
        Assert.Equal("آپارتمان ۹۰ متری در ونک، تهران برای فروش به قیمت ۵ میلیارد تومان؛ ۲ خواب، طبقهٔ ۳.", SeoText.Summary(p));
    }

    [Fact]
    public void ListIntro_reports_count_and_price_range()
    {
        var txt = SeoText.ListIntro("آشیانه", "خرید ملک", "تهران", 12, 4_000_000_000, 9_500_000_000, DealType.Sale);
        Assert.Equal("در حال حاضر ۱۲ آگهی خرید ملک در تهران در آشیانه منتشر شده است؛ قیمت‌ها از ۴ میلیارد تومان تا ۹٫۵ میلیارد تومان است.", txt);
    }

    [Fact]
    public void ListUrl_keeps_only_indexable_parameters_in_fixed_order()
    {
        Assert.Equal(Base + "/%D8%A7%D9%85%D9%84%D8%A7%DA%A9", SeoFiles.ListUrl(Base, null));
        Assert.Equal(Base + "/%D8%A7%D9%85%D9%84%D8%A7%DA%A9?deal=Rent&city=%D8%AA%D9%87%D8%B1%D8%A7%D9%86&p=2", SeoFiles.ListUrl(Base, DealType.Rent, " تهران ", 2));
    }

    [Fact]
    public void Sitemap_includes_city_list_pages_once()
    {
        var xml = SeoFiles.Sitemap([Sample("a"), Sample("b")], Base);
        var cityUrl = SeoFiles.ListUrl(Base, DealType.Sale, "تهران");
        Assert.Equal(1, xml.Split("<loc>" + cityUrl.Replace("&", "&amp;") + "</loc>").Length - 1);
    }

    [Fact]
    public void Persian_date_uses_tehran_time()
    {
        // 2026-10-25 21:00 UTC is already 26 Oct in Tehran = 1405/08/04.
        Assert.Equal("۱۴۰۵/۰۸/۰۴", Fa.Date(new DateTime(2026, 10, 25, 21, 0, 0, DateTimeKind.Utc)));
    }

    [Fact]
    public void Script_tag_has_literal_ld_json_type()
    {
        var html = JsonLd.Script(new Dictionary<string, string> { ["name"] = "x</script>" });
        Assert.StartsWith("<script type=\"application/ld+json\">", html);
        Assert.Equal(1, html.Split("</script>").Length - 1);
    }
}
