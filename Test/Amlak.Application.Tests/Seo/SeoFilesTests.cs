using System.Xml.Linq;
using Amlak.Rest.EndPoints.Seo;
using Listing.Application.Queries;

namespace Amlak.Application.Tests.Seo;

public sealed class SeoFilesTests
{
    private const string Base = "https://example.ir";

    private static SitemapEntry Entry(string slug = "آپارتمان-تست")
        => new(slug, "آپارتمان تست", new DateTime(2026, 10, 20, 0, 0, 0, DateTimeKind.Utc), "/api/v1/Photos/a.jpg");

    private static PropertyCard Card() => new(1, "آپارتمان-تست", "آپارتمان تست", "Sale", "Apartment",
        5_000_000_000, null, null, 90, 2, "تهران", "ونک", null, DateTime.UtcNow);

    private static XDocument Parse(string xml) => XDocument.Parse(xml[xml.IndexOf("<urlset", StringComparison.Ordinal)..]);

    [Fact]
    public void Sitemap_has_encoded_absolute_urls_lastmod_and_cover_image()
    {
        var doc  = Parse(SeoFiles.Sitemap([Entry()], Base));
        var locs = doc.Descendants().Where(e => e.Name.LocalName == "loc").Select(e => e.Value).ToList();

        locs.Should().Contain(Base + "/")
            .And.Contain(l => l.StartsWith(Base + "/%D9%85%D9%84%DA%A9/"))   // /ملک/ کدگذاری‌شده
            .And.Contain(Base + "/api/v1/Photos/a.jpg")
            .And.OnlyContain(l => l.StartsWith(Base));
        doc.Descendants().First(e => e.Name.LocalName == "lastmod").Value.Should().Be("2026-10-20");
    }

    [Fact]
    public void Sitemap_escapes_xml_special_characters_in_slugs()
    {
        var xml = SeoFiles.Sitemap([Entry("a&b")], Base);
        FluentActions.Invoking(() => Parse(xml)).Should().NotThrow();
        xml.Should().Contain("a%26b");
    }

    [Fact]
    public void Robots_blocks_admin_for_everyone_and_lists_sitemap_and_ai_bots()
    {
        var txt = SeoFiles.Robots(Base);

        txt.Should().Contain("Disallow: /admin").And.Contain("Disallow: /login")
           .And.Contain($"Sitemap: {Base}/sitemap.xml")
           .And.Contain("User-agent: GPTBot").And.Contain("User-agent: ClaudeBot");
        // گروه هر ربات هوش مصنوعی باید Disallow ها را تکرار کند
        (txt.Split("Disallow: /admin" + Environment.NewLine).Length - 1).Should().Be(2);
    }

    [Fact]
    public void Llms_lists_pages_and_listings_with_price()
    {
        var txt = SeoFiles.Llms("آشیانه", Base, [Card()]);

        txt.Should().StartWith("# آشیانه")
           .And.Contain("[آپارتمان تست](" + Base + "/%D9%85%D9%84%DA%A9/")
           .And.Contain("۵ میلیارد");
    }
}
