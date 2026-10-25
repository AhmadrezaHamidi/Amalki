using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<Property> Latest { get; private set; } = [];

    public async Task OnGetAsync() =>
        Latest = await db.Properties.AsNoTracking().Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1))
            .Where(p => p.Status == ListingStatus.Published)
            .OrderByDescending(p => p.PublishedAtUtc).Take(8).ToListAsync();
}
