using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Support inbox: ticket list on one side, the selected conversation and reply box on the other.</summary>
public class TicketsModel(AppDbContext db) : PageModel
{
    [BindProperty(SupportsGet = true)] public TicketStatus? Status { get; set; }
    [BindProperty(SupportsGet = true)] public int? Id { get; set; }

    [BindProperty, Required(ErrorMessage = "متن پاسخ را بنویسید"), StringLength(4000, ErrorMessage = "پاسخ حداکثر ۴۰۰۰ نویسه است")]
    public string Reply { get; set; } = "";

    public List<Ticket> Items { get; private set; } = [];
    public Ticket? Current { get; private set; }
    public Dictionary<TicketStatus, int> Counts { get; private set; } = [];

    public async Task OnGetAsync() => await LoadAsync();

    public async Task<IActionResult> OnPostReplyAsync(int id)
    {
        var t = await db.Tickets.FindAsync(id);
        if (t is null) return NotFound();
        if (!ModelState.IsValid)
        {
            Id = id;
            await LoadAsync();
            return Page();
        }
        db.TicketMessages.Add(new TicketMessage { TicketId = id, FromStaff = true, Body = Reply.Trim() });
        t.Status = TicketStatus.Answered;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id, Status });
    }

    public async Task<IActionResult> OnPostSetStatusAsync(int id, TicketStatus to)
    {
        var t = await db.Tickets.FindAsync(id);
        if (t is null) return NotFound();
        t.Status = to;
        t.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return RedirectToPage(new { id, Status });
    }

    private async Task LoadAsync()
    {
        Counts = await db.Tickets.GroupBy(t => t.Status).Select(g => new { g.Key, N = g.Count() }).ToDictionaryAsync(x => x.Key, x => x.N);
        var q = db.Tickets.AsNoTracking();
        if (Status is { } s) q = q.Where(t => t.Status == s);
        // Waiting-for-us first, then most recently active.
        Items = await q.OrderBy(t => t.Status).ThenByDescending(t => t.UpdatedAtUtc).Take(200).ToListAsync();
        var id = Id ?? Items.FirstOrDefault()?.Id;
        if (id is not null)
            Current = await db.Tickets.AsNoTracking().Include(t => t.Messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id))
                .FirstOrDefaultAsync(t => t.Id == id);
    }
}
