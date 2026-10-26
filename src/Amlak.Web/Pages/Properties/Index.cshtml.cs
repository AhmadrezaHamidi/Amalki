using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public class IndexModel(AppDbContext db) : PageModel
{
    private const int PageSize = 12;

    [BindProperty(SupportsGet = true)] public DealType? Deal { get; set; }
    [BindProperty(SupportsGet = true)] public PropertyKind? Kind { get; set; }
    [BindProperty(SupportsGet = true)] public string? City { get; set; }
    [BindProperty(SupportsGet = true)] public long? MaxPrice { get; set; }
    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;

    public List<Property> Items { get; private set; } = [];
    public int Total { get; private set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);
    public int FirstPosition => (P - 1) * PageSize + 1;

    /// <summary>Price range (sale price or monthly rent) of the filtered set; only computed when one deal type is selected.</summary>
    public long? MinFound { get; private set; }
    public long? MaxFound { get; private set; }

    /// <summary>Pager link that keeps the active filters and drops empty ones.</summary>
    public string PageUrl(int page)
    {
        var q = new Dictionary<string, string?>();
        if (Deal is { } d) q["deal"] = d.ToString();
        if (Kind is { } k) q["kind"] = k.ToString();
        if (!string.IsNullOrWhiteSpace(City)) q["city"] = City.Trim();
        if (MaxPrice is { } m) q["maxPrice"] = m.ToString();
        if (page > 1) q["p"] = page.ToString();
        return QueryHelpers.AddQueryString("/املاک", q);
    }

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
        if (MaxPrice is { } m) q = q.Where(p => (p.Price ?? p.MonthlyRent) <= m);

        Total = await q.CountAsync();
        if (Deal is not null && Total > 0)
        {
            var prices = q.Select(p => p.Deal == DealType.Sale ? p.Price : p.MonthlyRent).Where(x => x > 0);
            MinFound = await prices.MinAsync();
            MaxFound = await prices.MaxAsync();
        }
        P = Math.Max(1, P);
        Items = await q.Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .OrderByDescending(p => p.PublishedAtUtc).Skip((P - 1) * PageSize).Take(PageSize).ToListAsync();
    }
}
