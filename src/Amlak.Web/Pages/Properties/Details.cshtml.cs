using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public class DetailsModel(AppDbContext db) : PageModel
{
    public Property Item { get; private set; } = null!;
    public List<Property> Similar { get; private set; } = [];
    public bool Sent { get; private set; }

    public async Task<IActionResult> OnGetAsync(string slug, bool sent = false)
    {
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == ListingStatus.Published);
        if (p is null) return NotFound();
        Item = p;
        Sent = sent;
        // Same deal in the same city, same kind first.
        Similar = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder).Take(1))
            .Where(x => x.Status == ListingStatus.Published && x.Id != p.Id && x.Deal == p.Deal && x.City == p.City)
            .OrderByDescending(x => x.Kind == p.Kind).ThenByDescending(x => x.PublishedAtUtc)
            .Take(3).ToListAsync();
        return Page();
    }
}
