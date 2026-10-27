namespace Amlak.Web.Services;

/// <summary>A browsable listing category: a deal type, optionally narrowed to one kind of property.</summary>
public sealed record Category(string Slug, string Name, DealType Deal, PropertyKind? Kind);

/// <summary>
/// Divar-style landing pages: /املاک/{city}/{category}/{district}. Each combination is a real, indexable
/// page with its own title and canonical URL, instead of a query string on one list page.
/// </summary>
public static class Taxonomy
{
    public const string Root = "/املاک";

    /// <summary>City segment meaning "every city", like Divar's /s/iran.</summary>
    public const string AllCities = "ایران";

    public static readonly IReadOnlyList<Category> All =
    [
        new("خرید", "خرید و فروش ملک", DealType.Sale, null),
        new("خرید-آپارتمان", "خرید آپارتمان", DealType.Sale, PropertyKind.Apartment),
        new("خرید-ویلا", "خرید خانه و ویلا", DealType.Sale, PropertyKind.Villa),
        new("خرید-زمین", "خرید زمین و کلنگی", DealType.Sale, PropertyKind.Land),
        new("خرید-مغازه", "خرید مغازه و ملک تجاری", DealType.Sale, PropertyKind.Commercial),
        new("خرید-دفتر-اداری", "خرید دفتر اداری", DealType.Sale, PropertyKind.Office),
        new("رهن-و-اجاره", "رهن و اجارهٔ ملک", DealType.Rent, null),
        new("اجاره-آپارتمان", "رهن و اجارهٔ آپارتمان", DealType.Rent, PropertyKind.Apartment),
        new("اجاره-ویلا", "رهن و اجارهٔ خانه و ویلا", DealType.Rent, PropertyKind.Villa),
        new("اجاره-زمین", "اجارهٔ زمین", DealType.Rent, PropertyKind.Land),
        new("اجاره-مغازه", "رهن و اجارهٔ مغازه و ملک تجاری", DealType.Rent, PropertyKind.Commercial),
        new("اجاره-دفتر-اداری", "رهن و اجارهٔ دفتر اداری", DealType.Rent, PropertyKind.Office),
        new("اجاره-کوتاه-مدت", "اجارهٔ کوتاه‌مدت و روزانه", DealType.ShortTerm, null),
        new("اجاره-روزانه-آپارتمان", "اجارهٔ روزانهٔ آپارتمان و سوئیت", DealType.ShortTerm, PropertyKind.Apartment),
        new("اجاره-روزانه-ویلا", "اجارهٔ روزانهٔ ویلا و باغ", DealType.ShortTerm, PropertyKind.Villa),
        new("اجاره-موقت-دفتر", "اجارهٔ موقت دفتر کار", DealType.ShortTerm, PropertyKind.Office),
    ];

    public static Category? Find(string? slug) =>
        string.IsNullOrEmpty(slug) ? null : All.FirstOrDefault(c => c.Slug == Slugger.Make(slug));

    /// <summary>
    /// The category for a deal and kind; a kind without a deal has none. Kinds with no category of their own
    /// under a deal (short-term land, say) fall back to the deal's category.
    /// </summary>
    public static Category? For(DealType? deal, PropertyKind? kind) =>
        deal is null ? null : All.FirstOrDefault(c => c.Deal == deal && c.Kind == kind) ?? All.First(c => c.Deal == deal && c.Kind is null);

    public static string Slug(string name) => Slugger.Make(name);

    /// <summary>Unencoded path of a landing page. A district needs a city and a category, as on Divar.</summary>
    public static string Path(string? city, Category? category, string? district = null)
    {
        var hasCity = !string.IsNullOrWhiteSpace(city);
        if (!hasCity && category is null) return Root;
        var path = Root + "/" + (hasCity ? Slug(city!) : AllCities);
        if (category is null) return path;
        path += "/" + category.Slug;
        return hasCity && !string.IsNullOrWhiteSpace(district) ? path + "/" + Slug(district!) : path;
    }

    /// <summary>Same as <see cref="Path"/> with each segment percent-encoded, for sitemaps and absolute URLs.</summary>
    public static string EncodedPath(string? city, Category? category, string? district = null) =>
        string.Join("/", Path(city, category, district).Split('/').Select(Uri.EscapeDataString));

    /// <summary>Page heading: "خرید آپارتمان در سعادت‌آباد تهران".</summary>
    public static string Heading(Category? category, string? city, string? district)
    {
        var what = category?.Name ?? "آگهی‌های خرید، فروش و اجارهٔ ملک";
        var where = string.Join(" ", new[] { district, city }.Where(x => !string.IsNullOrWhiteSpace(x)));
        return where.Length == 0 ? what : $"{what} در {where}";
    }
}
