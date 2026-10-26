using System.Xml.Linq;
using Amlak.Web.Domain;
using Amlak.Web.Services.Seo;

namespace Amlak.Tests;

public class SeoTests
{
    private const string Base = "https://example.ir";

    private static Property Sample(string slug = "آپارتمان-تست")
    {
        var p = TestData.Listing(slug: slug);
        p.AddPhoto("/uploads/a.jpg", TestData.Now);
        return p;
    }

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
}
