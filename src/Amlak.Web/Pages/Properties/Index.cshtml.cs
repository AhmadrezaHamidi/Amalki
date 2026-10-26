using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public enum SortOrder { Newest, Cheapest, Priciest, Largest }

[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(AppDbContext db) : PageModel
{
    private const int PageSize = 12;

    [BindProperty(SupportsGet = true)] public DealType? Deal { get; set; }
    [BindProperty(SupportsGet = true)] public PropertyKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    // Strings, not numbers: visitors type Persian digits, which model binding would silently drop.
    [BindProperty(SupportsGet = true)] public string? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true)] public string? MinArea { get; set; }
    [BindProperty(SupportsGet = true)] public int? Beds { get; set; }
    [BindProperty(SupportsGet = true)] public SortOrder Sort { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<Property> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
    public long? MaxPriceValue => Number(MaxPrice);
    public int? MinAreaValue => Number(MinArea) is { } a and <= int.MaxValue ? (int)a : null;
    public bool HasFilters => Kind is not null || !string.IsNullOrWhiteSpace(City) || MaxPriceValue is not null || MinAreaValue is not null || Beds is not null;

    public async Task OnGetAsync()
    {
        var q = db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published);
        if (Deal is { } d) q = q.Where(p => p.Deal == d);
        if (Kind is { } k) q = q.Where(p => p.Kind == k);
        if (!string.IsNullOrWhiteSpace(City))
        {
            var city = City.Trim();
            q = q.Where(p => p.City == city);
        }
        // Sale is filtered by price, rent by deposit (rahn), which is what renters budget by.
        if (MaxPriceValue is { } m) q = q.Where(p => (p.Price ?? p.Deposit ?? 0) <= m);
        if (MinAreaValue is { } a) q = q.Where(p => p.AreaM2 >= a);
        if (Beds is { } b and > 0) q = q.Where(p => p.Bedrooms >= b);

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
    }

    /// <summary>Query string for the current filters with some values replaced; null drops a key.</summary>
    public string Link(params (string Key, object? Value)[] changes)
    {
        var values = new Dictionary<string, object?>
        {
            ["deal"] = Deal, ["kind"] = Kind, ["city"] = City, ["maxPrice"] = MaxPrice, ["minArea"] = MinArea,
            ["beds"] = Beds, ["sort"] = Sort == SortOrder.Newest ? null : Sort, ["p"] = null,
        };
        foreach (var (key, value) in changes) values[key] = value;
        var parts = values.Where(x => x.Value is not null && x.Value.ToString() != "")
            .Select(x => $"{x.Key}={Uri.EscapeDataString(x.Value!.ToString()!)}");
        return "?" + string.Join("&", parts);
    }

    private static long? Number(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var digits = new string(Fa.Latin(s).Where(char.IsAsciiDigit).ToArray());
        return digits.Length is > 0 and < 19 ? long.Parse(digits) : null;
    }
}
