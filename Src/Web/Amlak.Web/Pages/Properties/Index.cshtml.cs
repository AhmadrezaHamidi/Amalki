using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
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
        P = Math.Max(1, P);
        Items = await q.Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .OrderByDescending(p => p.PublishedAtUtc).Skip((P - 1) * PageSize).Take(PageSize).ToListAsync();
    }
}
