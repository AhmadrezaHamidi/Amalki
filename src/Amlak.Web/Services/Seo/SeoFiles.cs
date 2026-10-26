using System.Text;
using System.Xml.Linq;

namespace Amlak.Web.Services.Seo;

/// <summary>Pure builders for sitemap.xml, robots.txt and llms.txt so they can be unit-tested without a server.</summary>
public static class SeoFiles
{
    public const int MaxSitemapUrls = 50_000;

    // Crawlers of AI search/answer engines. Allowed on purpose so listings can be cited (GEO).
    private static readonly string[] AiCrawlers = ["GPTBot", "OAI-SearchBot", "ChatGPT-User", "ClaudeBot", "Claude-User", "PerplexityBot", "Google-Extended", "CCBot"];

    public static string ListUrl(string baseUrl) => $"{baseUrl}/{Uri.EscapeDataString("املاک")}";

    public static string PropertyUrl(string baseUrl, Property p) =>
        $"{baseUrl}/{Uri.EscapeDataString("ملک")}/{Uri.EscapeDataString(p.Slug)}";

    public static string Absolute(string baseUrl, string url) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : baseUrl + (url.StartsWith('/') ? url : "/" + url);

    /// <summary>
    /// Divar-style landing pages that have at least one published listing: every city, every
    /// city + category, and every city + category + district, plus the all-cities category pages.
    /// </summary>
    public static List<string> LandingPaths(IEnumerable<Property> published)
    {
        var list = published.ToList();
        var paths = new List<string>();
        foreach (var c in Taxonomy.All.Where(c => list.Any(p => Matches(c, p))))
            paths.Add(Taxonomy.EncodedPath(null, c));
        if (list.Count > 0) paths.Add(Amlak.Web.Pages.Prices.IndexModel.Path(null));
        foreach (var city in list.GroupBy(p => p.City).OrderByDescending(g => g.Count()))
        {
            paths.Add(Taxonomy.EncodedPath(city.Key, null));
            paths.Add(Amlak.Web.Pages.Prices.IndexModel.Path(city.Key));
            foreach (var c in Taxonomy.All.Where(c => city.Any(p => Matches(c, p))))
            {
                paths.Add(Taxonomy.EncodedPath(city.Key, c));
                foreach (var district in city.Where(p => Matches(c, p)).Select(p => p.District).Distinct())
                    paths.Add(Taxonomy.EncodedPath(city.Key, c, district));
            }
        }
        return paths;
    }

    private static bool Matches(Category c, Property p) => p.Deal == c.Deal && (c.Kind is null || p.Kind == c.Kind);

    public static string Sitemap(IEnumerable<Property> published, string baseUrl)
    {
        published = published.ToList();
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace img = "http://www.google.com/schemas/sitemap-image/1.1";
        var urls = new List<XElement>
        {
            new(ns + "url", new XElement(ns + "loc", baseUrl + "/"), new XElement(ns + "changefreq", "daily"), new XElement(ns + "priority", "1.0")),
            new(ns + "url", new XElement(ns + "loc", ListUrl(baseUrl)), new XElement(ns + "changefreq", "daily"), new XElement(ns + "priority", "0.9")),
        };
        urls.AddRange(LandingPaths(published).Select(path =>
            new XElement(ns + "url", new XElement(ns + "loc", baseUrl + path), new XElement(ns + "changefreq", "daily"), new XElement(ns + "priority", "0.8"))));

        foreach (var p in published.Take(MaxSitemapUrls - urls.Count))
        {
            var url = new XElement(ns + "url",
                new XElement(ns + "loc", PropertyUrl(baseUrl, p)),
                new XElement(ns + "lastmod", (p.PublishedAtUtc ?? p.CreatedAtUtc).ToString("yyyy-MM-dd")),
                new XElement(ns + "priority", "0.7"));
            foreach (var photo in p.Photos.OrderBy(x => x.SortOrder).Take(10))
                url.Add(new XElement(img + "image", new XElement(img + "loc", Absolute(baseUrl, photo.Url))));
            urls.Add(url);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "image", img), urls));
        return doc.Declaration + Environment.NewLine + doc.Root;
    }

    public static string Robots(string baseUrl)
    {
        var sb = new StringBuilder();
        sb.AppendLine("User-agent: *");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /Admin");
        sb.AppendLine("Disallow: /Account");
        sb.AppendLine();
        // Specific groups replace the * group for those bots, so repeat the Disallow lines.
        foreach (var bot in AiCrawlers) sb.AppendLine($"User-agent: {bot}");
        sb.AppendLine("Allow: /");
        sb.AppendLine("Disallow: /Admin");
        sb.AppendLine("Disallow: /Account");
        sb.AppendLine();
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        return sb.ToString();
    }

    public static string Llms(string siteName, string baseUrl, IEnumerable<Property> latest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {siteName}");
        sb.AppendLine();
        sb.AppendLine($"> {siteName} یک سایت آگهی املاک فارسی برای خرید، فروش، رهن و اجارهٔ آپارتمان، ویلا، زمین، ملک تجاری و اداری است. هر آگهی عکس، متراژ، محله، شهر و قیمت دارد.");
        sb.AppendLine();
        sb.AppendLine("## صفحه‌های اصلی");
        sb.AppendLine($"- [همهٔ آگهی‌ها]({ListUrl(baseUrl)}): فهرست کامل با فیلتر شهر، نوع ملک و قیمت");
        sb.AppendLine($"- [خرید ملک]({baseUrl}{Taxonomy.EncodedPath(null, Taxonomy.Find("خرید"))}): آگهی‌های فروش");
        sb.AppendLine($"- [رهن و اجاره]({baseUrl}{Taxonomy.EncodedPath(null, Taxonomy.Find("رهن-و-اجاره"))}): آگهی‌های رهن و اجاره");
        sb.AppendLine($"- آدرس صفحه‌ها: {baseUrl}/املاک/{{شهر}}/{{دسته}}/{{محله}}، مثلاً /املاک/تهران/خرید-آپارتمان/سعادت-آباد. دسته‌ها: {string.Join("، ", Taxonomy.All.Select(c => c.Slug))}");
        sb.AppendLine();
        sb.AppendLine("## جدیدترین آگهی‌ها");
        foreach (var p in latest)
        {
            var deal = p.Deal == DealType.Sale ? "فروش" : "اجاره";
            var price = p.Deal == DealType.Sale
                ? $"قیمت {Fa.Money(p.Price)} تومان"
                : $"رهن {Fa.Money(p.Deposit)} و اجاره {Fa.Money(p.MonthlyRent)} تومان";
            sb.AppendLine($"- [{p.Title}]({PropertyUrl(baseUrl, p)}): {deal}، {Fa.Digits(p.AreaM2.ToString())} متر، {p.District} {p.City}، {price}");
        }
        sb.AppendLine();
        sb.AppendLine("## یادداشت");
        sb.AppendLine("- مبالغ به تومان است. فقط آگهی‌های منتشرشده در این فهرست‌ها هستند.");
        sb.AppendLine($"- نقشهٔ سایت: {baseUrl}/sitemap.xml");
        return sb.ToString();
    }
}
