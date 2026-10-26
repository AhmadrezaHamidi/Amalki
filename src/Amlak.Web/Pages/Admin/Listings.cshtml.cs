using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

public class ListingsModel(AppDbContext db, PhotoStorage photos, ChannelPublisher publisher, TimeProvider clock) : PageModel
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
        await publisher.SyncAsync(id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var p = await db.Properties.Include(x => x.Photos).FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        p.MarkDeleted(clock.GetUtcNow().UtcDateTime);
        await publisher.RemoveAllAsync(id);
        var files = p.Photos.ToList();
        db.Properties.Remove(p);
        await db.SaveChangesAsync();
        // Files go only after the database no longer points at them.
        foreach (var f in files) await photos.DeleteAsync(f);
        return RedirectToPage();
    }
}
