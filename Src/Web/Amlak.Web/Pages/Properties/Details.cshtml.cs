using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Properties;

public class DetailsModel(AppDbContext db) : PageModel
{
    public Property Item { get; private set; } = null!;

    public async Task<IActionResult> OnGetAsync(string slug)
    {
        var p = await db.Properties.AsNoTracking().Include(x => x.Photos.OrderBy(f => f.SortOrder))
            .FirstOrDefaultAsync(x => x.Slug == slug && x.Status == ListingStatus.Published);
        if (p is null) return NotFound();
        Item = p;
        return Page();
    }
}
