using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class ListingsModel(AppDbContext db, TimeProvider clock) : PageModel
{
    public const int PageSize = 50;

    [BindProperty(SupportsGet = true)] public int P { get; set; } = 1;
    public List<Property> Items { get; private set; } = [];
    /// <summary>Cover photo URL by listing id; listings without photos are missing.</summary>
    public Dictionary<int, string> Covers { get; private set; } = [];
    public int Total { get; private set; }
    public int Pages => (int)Math.Ceiling(Total / (double)PageSize);

    // Paged: with 20k listings the unpaged table was a 46 MB page that took 3-7 s (loadtest/README.md).
    public async Task OnGetAsync()
    {
        Total = await db.Properties.CountAsync();
        P = Math.Clamp(P, 1, Math.Max(1, Pages));
        Items = await db.Properties.AsNoTracking().Include(p => p.ChannelPosts)
            .OrderByDescending(p => p.CreatedAtUtc).ThenByDescending(p => p.Id)
            .Skip((P - 1) * PageSize).Take(PageSize).AsSplitQuery().ToListAsync();
        // Covers in a second query by PropertyId: a filtered Include(Photos.Take(1)) numbers every photo in the table.
        var ids = Items.Select(p => p.Id).ToList();
        Covers = (await db.PropertyPhotos.AsNoTracking().Where(f => ids.Contains(f.PropertyId))
                .Select(f => new { f.PropertyId, f.SortOrder, f.Url }).ToListAsync())
            .GroupBy(f => f.PropertyId).ToDictionary(g => g.Key, g => g.MinBy(f => f.SortOrder)!.Url);
    }

    public async Task<IActionResult> OnPostTogglePublishAsync(int id)
    {
        var p = await db.Properties.FindAsync(id);
        if (p is null) return NotFound();
        try
        {
            p.TogglePublished(clock.GetUtcNow().UtcDateTime);
        }
        catch (DomainException ex)
        {
            // An incomplete draft cannot go live; send the admin to the form that names the missing field.
            TempData["Error"] = ex.Message;
            return RedirectToPage("Edit", new { id });
        }
        await db.SaveChangesAsync();
        return RedirectToPage(new { p = P });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var p = await db.Properties.Include(x => x.Photos).Include(x => x.ChannelPosts).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        // Channel messages and photo files are removed after the save, from the ListingDeleted event.
        p.MarkDeleted(clock.GetUtcNow().UtcDateTime);
        db.Properties.Remove(p);
        await db.SaveChangesAsync();
        return RedirectToPage(new { p = P });
    }
}
