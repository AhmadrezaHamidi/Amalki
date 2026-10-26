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

    public async Task OnGetAsync()
    {
        var published = db.Properties.AsNoTracking().Where(p => p.Status == ListingStatus.Published);
        Latest = await published.Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .OrderByDescending(p => p.PublishedAtUtc).Take(8).ToListAsync();
        CountByKind = await published.GroupBy(p => p.Kind).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
