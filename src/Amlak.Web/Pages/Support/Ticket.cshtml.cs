using System.ComponentModel.DataAnnotations;
using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Support;

/// <summary>One ticket's conversation. The unguessable tracking code in the URL is the visitor's key to it.</summary>
public class TicketModel(AppDbContext db, AdminNotifier notifier, TimeProvider clock) : PageModel
{
    // HTTP headers are ASCII-only, so the Persian path segment is percent-encoded.
    public static string Url(string code, bool created = false) =>
        $"/{Uri.EscapeDataString("پشتیبانی")}/{code}" + (created ? "?created=true" : "");

    public Ticket Item { get; private set; } = null!;
    public bool Created { get; private set; }

    [BindProperty, Required(ErrorMessage = "پیام را بنویسید"), StringLength(4000, ErrorMessage = "پیام حداکثر ۴۰۰۰ نویسه است")]
    public string Reply { get; set; } = "";

    public async Task<IActionResult> OnGetAsync(string code, bool created = false)
    {
        if (await LoadAsync(code) is null) return NotFound();
        Created = created;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string code)
    {
        var t = await LoadAsync(code, tracking: true);
        if (t is null) return NotFound();
        if (!ModelState.IsValid) return Page();

        string body;
        try
        {
            body = t.ReplyFromVisitor(Reply, clock.GetUtcNow().UtcDateTime).Body;
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError("", ex.Message);
            return Page();
        }
        await db.SaveChangesAsync();
        await notifier.NotifyTicketAsync(t, body, isNew: false);
        return Redirect(Url(t.Code) + "#last");
    }

    private async Task<Ticket?> LoadAsync(string code, bool tracking = false)
    {
        var normalized = TicketCodes.Normalize(code);
        var q = db.Tickets.Include(x => x.Messages.OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id)).AsQueryable();
        if (!tracking) q = q.AsNoTracking();
        var t = await q.FirstOrDefaultAsync(x => x.Code == normalized);
        if (t is not null) Item = t;
        return t;
    }
}
