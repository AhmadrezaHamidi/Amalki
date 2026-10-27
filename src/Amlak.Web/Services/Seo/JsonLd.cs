using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Amlak.Web.Services.Seo;

public static class JsonLd
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Serializes for embedding in a script tag; "&lt;/" is escaped so content cannot close the tag.</summary>
    public static string Serialize(object data) => JsonSerializer.Serialize(data, Options).Replace("</", "<\\/");

    /// <summary>
    /// Complete script element, rendered with Html.Raw. A literal &lt;script type="application/ld+json"&gt; in Razor
    /// goes through tag helpers and comes out as "ld&amp;#x2B;json", which naive parsers do not recognise.
    /// </summary>
    public static string Script(object data) => $"<script type=\"application/ld+json\">{Serialize(data)}</script>";

    public static object Breadcrumbs(params (string Name, string Url)[] items) => new Dictionary<string, object>
    {
        ["@context"] = "https://schema.org",
        ["@type"] = "BreadcrumbList",
        ["itemListElement"] = items.Select((x, i) => new Dictionary<string, object>
        {
            ["@type"] = "ListItem",
            ["position"] = i + 1,
            ["name"] = x.Name,
            ["item"] = x.Url,
        }).ToArray(),
    };

    /// <summary>Organization + WebSite for the home page. Only facts we actually have; no invented contact data.</summary>
    public static object[] Site(string name, string baseUrl) =>
    [
        new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "RealEstateAgent",
            ["name"] = name, ["url"] = baseUrl + "/", ["areaServed"] = "IR", ["inLanguage"] = "fa",
        },
        new Dictionary<string, object>
        {
            ["@context"] = "https://schema.org", ["@type"] = "WebSite",
            ["name"] = name, ["url"] = baseUrl + "/", ["inLanguage"] = "fa",
        },
    ];

    /// <summary>
    /// RealEstateListing with an Offer for the place. Prices are stored in Toman, which has no ISO 4217 code,
    /// so they are emitted as IRR (Toman x 10). Exact address and coordinates are left out on purpose.
    /// </summary>
    public static object Listing(Property p, string baseUrl)
    {
        var url = SeoFiles.PropertyUrl(baseUrl, p);
        var sale = p.Deal == DealType.Sale;
        var accommodation = p.Kind is PropertyKind.Apartment or PropertyKind.Villa;
        var area = Obj(("@type", "QuantitativeValue"), ("value", p.AreaM2), ("unitCode", "MTK"));

        var place = Obj(
            ("@type", p.Kind switch { PropertyKind.Apartment => "Apartment", PropertyKind.Villa => "House", _ => "Place" }),
            ("name", p.Title),
            // floorSize/numberOfBedrooms exist only on Accommodation; other kinds carry the area as a property value.
            ("floorSize", accommodation ? area : null),
            ("numberOfBedrooms", accommodation ? p.Bedrooms : null),
            ("additionalProperty", accommodation ? null : new[] { Obj(("@type", "PropertyValue"), ("name", "متراژ"), ("value", p.AreaM2), ("unitCode", "MTK")) }),
            ("address", Obj(("@type", "PostalAddress"), ("addressLocality", p.City), ("addressCountry", "IR"))),
            ("containedInPlace", string.IsNullOrWhiteSpace(p.District) ? null : Obj(("@type", "Place"), ("name", p.District))));

        var rent = p.Deal == DealType.Rent;
        var price = Rial(rent ? p.MonthlyRent : p.Price);
        var offer = Obj(
            ("@type", "Offer"),
            ("businessFunction", sale ? "http://purl.org/goodrelations/v1#Sell" : "http://purl.org/goodrelations/v1#LeaseOut"),
            ("price", price),
            ("priceCurrency", price is null ? null : "IRR"),
            ("priceSpecification", p.Deal switch { DealType.Rent => RentSpecification(p), DealType.ShortTerm => NightSpecification(p), _ => null }),
            ("availability", "https://schema.org/InStock"),
            ("url", url),
            ("itemOffered", place));

        var images = p.Photos.OrderBy(x => x.SortOrder).Select(x => SeoFiles.Absolute(baseUrl, x.Url)).ToArray();
        return Obj(
            ("@context", "https://schema.org"),
            ("@type", "RealEstateListing"),
            ("name", p.Title),
            ("description", string.IsNullOrWhiteSpace(p.Description) ? SeoText.Summary(p) : p.Description),
            ("url", url),
            ("datePosted", p.PublishedAtUtc?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            ("image", images.Length == 0 ? null : images),
            ("inLanguage", "fa"),
            ("offers", offer));
    }

    /// <summary>ItemList of the listings shown on a list page; positions continue across pages.</summary>
    public static object ItemList(IEnumerable<Property> items, string baseUrl, int firstPosition = 1) =>
        ItemList(items.Select(p => (p.Slug, p.Title)), baseUrl, firstPosition);

    /// <summary>The list pages read cards from the read side (vw_ListingCards).</summary>
    public static object ItemList(IEnumerable<Read.ListingCard> items, string baseUrl, int firstPosition = 1) =>
        ItemList(items.Select(c => (c.Slug, c.Title)), baseUrl, firstPosition);

    private static object ItemList(IEnumerable<(string Slug, string Title)> items, string baseUrl, int firstPosition) => Obj(
        ("@context", "https://schema.org"),
        ("@type", "ItemList"),
        ("itemListElement", items.Select((p, i) => Obj(
            ("@type", "ListItem"), ("position", firstPosition + i), ("url", SeoFiles.PropertyUrl(baseUrl, p.Slug)), ("name", p.Title))).ToArray()));

    private static object[]? RentSpecification(Property p)
    {
        var specs = new List<object>();
        if (Rial(p.MonthlyRent) is { } rent)
            specs.Add(Obj(("@type", "UnitPriceSpecification"), ("name", "اجارهٔ ماهانه"), ("price", rent), ("priceCurrency", "IRR"), ("unitCode", "MON")));
        if (Rial(p.Deposit) is { } deposit)
            specs.Add(Obj(("@type", "PriceSpecification"), ("name", "رهن (ودیعه)"), ("price", deposit), ("priceCurrency", "IRR")));
        return specs.Count == 0 ? null : specs.ToArray();
    }

    private static object[]? NightSpecification(Property p) => Rial(p.Price) is { } night
        ? [Obj(("@type", "UnitPriceSpecification"), ("name", "اجارهٔ هر شب"), ("price", night), ("priceCurrency", "IRR"), ("unitCode", "DAY"))]
        : null;

    private static long? Rial(long? toman) => toman is null or <= 0 ? null : toman * 10;

    /// <summary>Dictionary without null values; the serializer's ignore-null option does not apply to dictionary entries.</summary>
    private static Dictionary<string, object> Obj(params (string Key, object? Value)[] props)
    {
        var d = new Dictionary<string, object>();
        foreach (var (k, v) in props)
            if (v is not null) d[k] = v;
        return d;
    }
}
