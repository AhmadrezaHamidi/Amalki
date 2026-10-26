using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Contact requests from visitors (ADM-10).</summary>
public class LeadsModel(AppDbContext db) : PageModel
{
    public List<Lead> Items { get; private set; } = [];
    [BindProperty(SupportsGet = true)] public bool All { get; set; }

    public async Task OnGetAsync()
    {
        var q = db.Leads.AsNoTracking().Include(l => l.Property).AsQueryable();
        if (!All) q = q.Where(l => !l.Handled);
        Items = await q.OrderByDescending(l => l.CreatedAtUtc).Take(500).ToListAsync();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        lead.Handled = !lead.Handled;
        await db.SaveChangesAsync();
        return RedirectToPage(new { All });
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var lead = await db.Leads.FindAsync(id);
        if (lead is null) return NotFound();
        db.Leads.Remove(lead);
        await db.SaveChangesAsync();
        return RedirectToPage(new { All });
    }
}
