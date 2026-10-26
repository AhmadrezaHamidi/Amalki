using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class ListingsModel(AppDbContext db, PhotoStorage photos, ChannelPublisher publisher) : PageModel
{
    public List<Property> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Items = await db.Properties.AsNoTracking().Include(p => p.Photos.OrderBy(x => x.SortOrder).Take(1)).Include(p => p.ChannelPosts)
            .OrderByDescending(p => p.CreatedAtUtc).ToListAsync();
    }

    public async Task<IActionResult> OnPostTogglePublishAsync(int id)
    {
        var p = await db.Properties.FindAsync(id);
        if (p is null) return NotFound();
        if (p.Status == ListingStatus.Published)
        {
            p.Status = ListingStatus.Draft;
        }
        else
        {
            p.Status = ListingStatus.Published;
            p.PublishedAtUtc ??= DateTime.UtcNow;
        }
        await db.SaveChangesAsync();
        await publisher.SyncAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var p = await db.Properties.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        await publisher.RemoveAllAsync(id);
        foreach (var f in p.Photos) await photos.DeleteAsync(f);
        db.Properties.Remove(p);
        await db.SaveChangesAsync();
        return RedirectToPage();
    }
}
