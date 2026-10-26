using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public class DetailsModel(AppDbContext db) : PageModel
{
    public Property Item { get; private set; } = null!;
    public List<Property> Similar { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == ListingStatus.Published);
        if (p is null) return NotFound();
        Item = p;
        // Internal links to listings of the same deal in the same city (helps crawl depth and users).
        Similar = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder).Take(1))
            .Where(x => x.Status == ListingStatus.Published && x.Id != p.Id && x.Deal == p.Deal && x.City == p.City)
            .OrderByDescending(x => x.PublishedAtUtc).Take(4).ToListAsync();
        return Page();
    }
}
