using System.Text;
using System.Xml.Linq;
using Amlak.Application.Contract.Common;

namespace Amlak.Rest.EndPoints.Seo;

/// <summary>
/// ساخت sitemap.xml، robots.txt و llms.txt — تابع خالص تا بدون سرور تست شود.
/// مسیرهای صفحه (<c>/املاک</c>، <c>/ملک/{slug}</c>) همان مسیرهای فرانت React است.
/// </summary>
public static class SeoFiles
{
    public const int MaxSitemapUrls = 50_000;

    // خزنده‌های موتورهای جست‌وجو/پاسخ هوش مصنوعی — عمداً مجازند تا آگهی‌ها نقل شوند (GEO)
    private static readonly string[] AiCrawlers = ["GPTBot", "OAI-SearchBot", "ChatGPT-User", "ClaudeBot", "Claude-User", "PerplexityBot", "Google-Extended", "CCBot"];

    public static string ListUrl(string baseUrl) => $"{baseUrl}/{Uri.EscapeDataString("املاک")}";

    public static string PropertyUrl(string baseUrl, string slug) =>
        $"{baseUrl}/{Uri.EscapeDataString("ملک")}/{Uri.EscapeDataString(slug)}";

    public static string Absolute(string baseUrl, string url) =>
        url.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? url : baseUrl + (url.StartsWith('/') ? url : "/" + url);

    public static string Sitemap(IEnumerable<SitemapEntry> published, string baseUrl)
    {
        XNamespace ns  = "http://www.sitemaps.org/schemas/sitemap/0.9";
        XNamespace img = "http://www.google.com/schemas/sitemap-image/1.1";
        var urls = new List<XElement>
        {
            Page(baseUrl + "/", "daily", "1.0"),
            Page(ListUrl(baseUrl), "daily", "0.9"),
            Page(ListUrl(baseUrl) + "?deal=Sale", "daily", "0.8"),
            Page(ListUrl(baseUrl) + "?deal=Rent", "daily", "0.8"),
        };

        foreach (var p in published.Take(MaxSitemapUrls - urls.Count))
        {
            var url = new XElement(ns + "url",
                new XElement(ns + "loc", PropertyUrl(baseUrl, p.Slug)),
                new XElement(ns + "lastmod", p.LastModified.ToString("yyyy-MM-dd")),
                new XElement(ns + "priority", "0.7"));
            if (p.CoverPhotoUrl is { } cover)
                url.Add(new XElement(img + "image", new XElement(img + "loc", Absolute(baseUrl, cover))));
            urls.Add(url);
        }

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement(ns + "urlset", new XAttribute(XNamespace.Xmlns + "image", img), urls));
        return doc.Declaration + Environment.NewLine + doc.Root;

        XElement Page(string loc, string freq, string priority) => new(ns + "url",
            new XElement(ns + "loc", loc), new XElement(ns + "changefreq", freq), new XElement(ns + "priority", priority));
    }

    public static string Robots(string baseUrl)
    {
        var sb = new StringBuilder();
        AppendGroup(sb, ["*"]);
        // گروه اختصاصی جای گروه * را برای آن ربات‌ها می‌گیرد، پس Disallow ها تکرار می‌شوند
        AppendGroup(sb, AiCrawlers);
        sb.AppendLine($"Sitemap: {baseUrl}/sitemap.xml");
        return sb.ToString();

        static void AppendGroup(StringBuilder sb, IEnumerable<string> agents)
        {
            foreach (var bot in agents) sb.AppendLine($"User-agent: {bot}");
            sb.AppendLine("Allow: /");
            sb.AppendLine("Disallow: /admin");
            sb.AppendLine("Disallow: /login");
            sb.AppendLine("Disallow: /api/v1/Admin");
            sb.AppendLine();
        }
    }

    public static string Llms(string siteName, string baseUrl, IEnumerable<PropertyCard> latest)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# {siteName}");
        sb.AppendLine();
        sb.AppendLine($"> {siteName} یک سایت آگهی املاک فارسی برای خرید، فروش، رهن و اجارهٔ آپارتمان، ویلا، زمین، ملک تجاری و اداری است. هر آگهی عکس، متراژ، محله، شهر و قیمت دارد.");
        sb.AppendLine();
        sb.AppendLine("## صفحه‌های اصلی");
        sb.AppendLine($"- [همهٔ آگهی‌ها]({ListUrl(baseUrl)}): فهرست کامل با فیلتر شهر، محله، نوع ملک، متراژ و قیمت");
        sb.AppendLine($"- [خرید ملک]({ListUrl(baseUrl)}?deal=Sale): آگهی‌های فروش");
        sb.AppendLine($"- [اجاره و رهن]({ListUrl(baseUrl)}?deal=Rent): آگهی‌های رهن و اجاره");
        sb.AppendLine();
        sb.AppendLine("## جدیدترین آگهی‌ها");
        foreach (var p in latest)
        {
            var isSale = p.Deal == "Sale";
            var price = isSale
                ? $"قیمت {Fa.Money(p.Price)} تومان"
                : $"رهن {Fa.Money(p.Deposit)} و اجاره {Fa.Money(p.MonthlyRent)} تومان";
            sb.AppendLine($"- [{p.Title}]({PropertyUrl(baseUrl, p.Slug)}): {(isSale ? "فروش" : "اجاره")}، {Fa.Digits(p.AreaM2.ToString())} متر، {p.District} {p.City}، {price}");
        }
        sb.AppendLine();
        sb.AppendLine("## یادداشت");
        sb.AppendLine("- مبالغ به تومان است. فقط آگهی‌های منتشرشده در این فهرست‌ها هستند.");
        sb.AppendLine($"- نقشهٔ سایت: {baseUrl}/sitemap.xml");
        return sb.ToString();
    }
}
