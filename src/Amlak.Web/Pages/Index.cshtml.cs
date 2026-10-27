using Amlak.Web.Data;
using Amlak.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

[OutputCache(PolicyName = SiteCache.Listings)]
public class IndexModel(AppDbContext db) : PageModel
{
    public List<Property> Latest { get; private set; } = [];
    public Dictionary<PropertyKind, int> CountByKind { get; private set; } = [];
    /// <summary>Busiest districts, for the "popular areas" chips.</summary>
    public List<(string City, string District, int Count)> TopAreas { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var published = db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published);
        Latest = await published.Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .OrderByDescending(p => p.PublishedAtUtc).Take(8).ToListAsync();
        CountByKind = await published.GroupBy(p => p.Kind).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        TopAreas = (await published.GroupBy(p => new { p.City, p.District })
                .Select(g => new { g.Key.City, g.Key.District, Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(12).ToListAsync())
            .Select(x => (x.City, x.District, x.Count)).ToList();
    }
}
