using System.Globalization;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public enum SortOrder { Newest, Cheapest, Priciest, Largest }

/// <summary>
/// The listing page, served both as /املاک and as Divar-style landing pages /املاک/{city}/{category}/{district}.
/// City, deal, kind and district live in the path; every other filter stays in the query string.
/// </summary>
[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(AppDbContext db) : PageModel
{
    private const int PageSize = 12;

    // Keys that the path encodes. When a form or old link sends them as query values, we redirect to the path.
    private static readonly string[] PathKeys = ["deal", "kind", "city", "district"];

    [BindProperty(SupportsGet = true)] public DealType? Deal { get; set; }
    [BindProperty(SupportsGet = true)] public PropertyKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    [BindProperty(SupportsGet = true)] public string? District { get; set; }
    // Strings, not numbers: visitors type Persian digits, which model binding would silently drop.
    [BindProperty(SupportsGet = true)] public string? MinPrice { get; set; }
    [BindProperty(SupportsGet = true)] public string? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true)] public string? MaxRent { get; set; }
    [BindProperty(SupportsGet = true)] public string? MinArea { get; set; }
    [BindProperty(SupportsGet = true)] public string? MaxArea { get; set; }
    [BindProperty(SupportsGet = true)] public int? Beds { get; set; }
    /// <summary>Maximum building age in years.</summary>
    [BindProperty(SupportsGet = true)] public int? Age { get; set; }
    [BindProperty(SupportsGet = true)] public bool Elevator { get; set; }
    [BindProperty(SupportsGet = true)] public bool Parking { get; set; }
    [BindProperty(SupportsGet = true)] public bool Storage { get; set; }
    [BindProperty(SupportsGet = true)] public bool Photo { get; set; }
    [BindProperty(SupportsGet = true)] public bool Convertible { get; set; }
    [BindProperty(SupportsGet = true)] public SortOrder Sort { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<Property> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
    public Category? Category => Taxonomy.For(Deal, Kind);
    /// <summary>The landing page this view belongs to, without query filters.</summary>
    public string BasePath => Taxonomy.Path(City, Category, District);
    public string Heading => Taxonomy.Heading(Category, City, Areas.Count > 0 ? string.Join("، ", Areas) : District);
    /// <summary>Published districts of the current city with their listing counts, for the neighbourhood links.</summary>
    public List<(string Name, int Count)> Districts { get; private set; } = [];
    /// <summary>Two or more districts picked together (Divar's multi-neighbourhood filter); they stay in the query.</summary>
    public List<string> Areas { get; private set; } = [];
    /// <summary>Published cities with counts, shown when no city is chosen.</summary>
    public List<(string Name, int Count)> Cities { get; private set; } = [];
    /// <summary>Average sale price per square metre for this city/district/kind (Melkradar-style), when there are sale listings.</summary>
    public long? AvgPricePerM2 { get; private set; }
    /// <summary>A path segment matched no published city or district: render, but keep it out of the index.</summary>
    public bool UnknownPlace { get; private set; }

    public long? MinPriceValue => Number(MinPrice);
    public long? MaxPriceValue => Number(MaxPrice);
    public long? MaxRentValue => Number(MaxRent);
    public int? MinAreaValue => Number(MinArea) is { } a and <= int.MaxValue ? (int)a : null;
    public int? MaxAreaValue => Number(MaxArea) is { } a and <= int.MaxValue ? (int)a : null;
    public bool HasFilters => QueryFilters().Any(x => x.Key is not ("sort" or "p") && x.Value is not null);

    public async Task<IActionResult> OnGetAsync(string? citySlug, string? categorySlug, string? districtSlug)
    {
        // Path segments fill whatever the query string did not send; query values win so filter forms work.
        var query = Request.Query;
        if (categorySlug is not null)
        {
            var category = Taxonomy.Find(categorySlug);
            if (category is null) return NotFound();
            if (!query.ContainsKey("deal")) Deal = category.Deal;
            if (!query.ContainsKey("kind")) Kind = category.Kind;
        }

        var places = await db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published)
            .GroupBy(p => new { p.City, p.District, p.Deal, p.Kind })
            .Select(g => new { g.Key.City, g.Key.District, g.Key.Deal, g.Key.Kind, Count = g.Count() })
            .ToListAsync();
        if (citySlug is not null && citySlug != Taxonomy.AllCities && !query.ContainsKey("city"))
        {
            var slug = Taxonomy.Slug(citySlug);
            City = places.FirstOrDefault(x => Taxonomy.Slug(x.City) == slug)?.City;
            if (City is null) { City = citySlug.Replace('-', ' '); UnknownPlace = true; }
        }
        if (districtSlug is not null && !query.ContainsKey("district"))
        {
            var slug = Taxonomy.Slug(districtSlug);
            District = places.FirstOrDefault(x => x.City == City && Taxonomy.Slug(x.District) == slug)?.District;
            if (District is null) { District = districtSlug.Replace('-', ' '); UnknownPlace = true; }
        }
        var picked = query["district"].Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()).Distinct().ToList();
        if (query.ContainsKey("district"))
        {
            // The form always sends an empty "district" so unticking every box clears a district in the path.
            District = picked.Count == 1 ? picked[0] : null;
            Areas = picked.Count > 1 ? picked : [];
        }
        City = string.IsNullOrWhiteSpace(City) ? null : City.Trim();
        District = City is null || string.IsNullOrWhiteSpace(District) ? null : District.Trim();
        if (City is null) Areas = [];

        // One URL per landing page: /املاک?city=تهران&deal=Sale becomes /املاک/تهران/خرید.
        var target = BasePath;
        // Several districts cannot live in the path, so only a single one counts as a path key.
        if (PathKeys.Any(k => query.ContainsKey(k) && (k != "district" || picked.Count <= 1)) || !string.Equals(Request.Path.Value?.TrimEnd('/'), target, StringComparison.Ordinal))
            return RedirectPermanent(Href(target, QueryFilters()));

        // Only link places that have listings in this category, with their counts.
        var inCategory = places.Where(x => (Deal == null || x.Deal == Deal) && (Kind == null || x.Kind == Kind)).ToList();
        Districts = City is null ? [] : inCategory.Where(x => x.City == City).GroupBy(x => x.District)
            .Select(g => (g.Key, g.Sum(x => x.Count))).OrderByDescending(x => x.Item2).ToList();
        Cities = inCategory.GroupBy(x => x.City).Select(g => (g.Key, g.Sum(x => x.Count))).OrderByDescending(x => x.Item2).ToList();

        var q = Apply(db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published));
        Total = await q.CountAsync();
        P = Math.Clamp(P, 1, Math.Max(1, Pages));
        q = Sort switch
        {
            SortOrder.Cheapest => q.OrderBy(p => p.Price ?? p.Deposit ?? 0),
            SortOrder.Priciest => q.OrderByDescending(p => p.Price ?? p.Deposit ?? 0),
            SortOrder.Largest => q.OrderByDescending(p => p.AreaM2),
            _ => q.OrderByDescending(p => p.PublishedAtUtc),
        };
        Items = await q.Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .Skip((P - 1) * PageSize).Take(PageSize).ToListAsync();

        if (Deal != DealType.Rent && City is not null)
        {
            var sales = await db.Properties.AsNoTracking()
                .Where(p => p.Status == ListingStatus.Published && p.Deal == DealType.Sale && p.City == City
                            && (District == null || p.District == District) && (Areas.Count == 0 || Areas.Contains(p.District))
                            && (Kind == null || p.Kind == Kind)
                            && p.Price > 0 && p.AreaM2 > 0)
                .Select(p => new { p.Price, p.AreaM2 }).ToListAsync();
            if (sales.Count > 0) AvgPricePerM2 = (long)sales.Average(s => (double)s.Price!.Value / s.AreaM2);
        }
        return Page();
    }

    /// <summary>All listing filters in one place, so the query can move to a read model unchanged.</summary>
    public IQueryable<Property> Apply(IQueryable<Property> q)
    {
        if (Deal is { } d) q = q.Where(p => p.Deal == d);
        if (Kind is { } k) q = q.Where(p => p.Kind == k);
        if (City is { } city) q = q.Where(p => p.City == city);
        if (District is { } district) q = q.Where(p => p.District == district);
        if (Areas.Count > 0) q = q.Where(p => Areas.Contains(p.District));
        // Sale is filtered by price, rent by deposit (rahn), which is what renters budget by.
        if (MinPriceValue is { } min) q = q.Where(p => (p.Price ?? p.Deposit ?? 0) >= min);
        if (MaxPriceValue is { } max) q = q.Where(p => (p.Price ?? p.Deposit ?? 0) <= max);
        if (MaxRentValue is { } rent) q = q.Where(p => (p.MonthlyRent ?? 0) <= rent);
        if (MinAreaValue is { } a) q = q.Where(p => p.AreaM2 >= a);
        if (MaxAreaValue is { } b) q = q.Where(p => p.AreaM2 <= b);
        if (Beds is { } beds and > 0) q = q.Where(p => p.Bedrooms >= beds);
        if (Age is { } age and >= 0)
        {
            var since = new PersianCalendar().GetYear(DateTime.Now) - age;
            q = q.Where(p => p.YearBuilt >= since);
        }
        if (Elevator) q = q.Where(p => p.HasElevator == true);
        if (Parking) q = q.Where(p => p.Parking > 0);
        if (Storage) q = q.Where(p => p.HasStorage == true);
        if (Photo) q = q.Where(p => p.Photos.Any());
        if (Convertible) q = q.Where(p => p.Convertible);
        return q;
    }

    /// <summary>Filters that stay in the query string (everything the path does not encode).</summary>
    public Dictionary<string, object?> QueryFilters() => new()
    {
        // A kind with no category page of its own (no deal, or none under this deal) stays a query value.
        ["kind"] = Category?.Kind != Kind ? Kind : null,
        ["district"] = Areas.Count > 1 ? Areas.ToArray() : null,
        ["minPrice"] = MinPrice, ["maxPrice"] = MaxPrice, ["maxRent"] = MaxRent, ["minArea"] = MinArea, ["maxArea"] = MaxArea,
        ["beds"] = Beds is > 0 ? Beds : null, ["age"] = Age,
        ["elevator"] = Elevator ? "true" : null, ["parking"] = Parking ? "true" : null, ["storage"] = Storage ? "true" : null,
        ["photo"] = Photo ? "true" : null, ["convertible"] = Convertible ? "true" : null,
        ["sort"] = Sort == SortOrder.Newest ? null : Sort, ["p"] = P > 1 ? P : null,
    };

    /// <summary>Link to this page with some query filters replaced; null drops a key. Page number resets.</summary>
    public string Link(params (string Key, object? Value)[] changes)
    {
        var values = QueryFilters();
        values["p"] = null;
        foreach (var (key, value) in changes) values[key] = value;
        return Href(BasePath, values);
    }

    /// <summary>Link to another landing page, keeping the current query filters.</summary>
    public string LinkTo(string? city, Category? category, string? district)
    {
        var values = QueryFilters();
        values["p"] = null;
        if (category is not null) values["kind"] = null;
        return Href(Taxonomy.Path(city, category, district), values);
    }

    private static string Href(string path, Dictionary<string, object?> values)
    {
        var parts = values.Where(x => x.Value is not null)
            .SelectMany(x => (x.Value as string[] ?? [x.Value!.ToString()!]).Select(v => (x.Key, Value: v)))
            .Where(x => x.Value != "")
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value)}").ToList();
        var encoded = string.Join("/", path.Split('/').Select(Uri.EscapeDataString));
        return parts.Count == 0 ? encoded : encoded + "?" + string.Join("&", parts);
    }

    private static long? Number(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(Fa.Latin(s).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is > 0 and < 19 ? long.Parse(digits) : null;
    }
}
