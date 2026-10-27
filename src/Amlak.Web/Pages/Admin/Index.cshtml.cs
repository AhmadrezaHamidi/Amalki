using Amlak.Web.Data;
using Amlak.Web.Services;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Dashboard: what is waiting for the admin today.</summary>
public class IndexModel(AppDbContext db, ChannelPublisher publisher, PhotoStorage photos) : PageModel
{
    public int Published { get; private set; }
    public int Drafts { get; private set; }
    public int NewLeads { get; private set; }
    public int OpenTickets { get; private set; }
    public int AnsweredTickets { get; private set; }
    public int ChannelErrors { get; private set; }
    /// <summary>Saved events not yet handled (waiting or retrying), and those that gave up after every retry.</summary>
    public int EventsPending { get; private set; }
    public int EventsFailed { get; private set; }
    public List<Ticket> Tickets { get; private set; } = [];
    public List<Lead> Leads { get; private set; } = [];
    public IReadOnlyCollection<Channel> Channels => publisher.ConfiguredChannels;
    /// <summary>Photos uploaded before WebP copies existed.</summary>
    public int PhotosWithoutCopies { get; private set; }
    [TempData] public string? Notice { get; set; }

    private const int BackfillBatch = 40;

    public async Task OnGetAsync()
    {
        Published = await db.Properties.CountAsync(p => p.Status == ListingStatus.Published);
        Drafts = await db.Properties.CountAsync(p => p.Status == ListingStatus.Draft);
        NewLeads = await db.Leads.CountAsync(l => !l.Handled);
        OpenTickets = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Open);
        AnsweredTickets = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Answered);
        ChannelErrors = await db.ChannelPosts.CountAsync(c => c.Error != null);
        EventsPending = await db.OutboxMessages.CountAsync(m => m.ProcessedAtUtc == null && m.Attempts < Amlak.Web.Services.Events.OutboxDispatcher.MaxAttempts);
        EventsFailed = await db.OutboxMessages.CountAsync(m => m.ProcessedAtUtc == null && m.Attempts >= Amlak.Web.Services.Events.OutboxDispatcher.MaxAttempts);
        PhotosWithoutCopies = await db.PropertyPhotos.CountAsync(x => x.Variants == null);
        Tickets = await db.Tickets.AsNoTracking().Where(t => t.Status != TicketStatus.Closed)
            .OrderBy(t => t.Status).ThenByDescending(t => t.UpdatedAtUtc).Take(6).ToListAsync();
        Leads = await db.Leads.AsNoTracking().Include(l => l.Property).Where(l => !l.Handled)
            .OrderByDescending(l => l.CreatedAtUtc).Take(6).ToListAsync();
    }

    /// <summary>Makes WebP copies for old photos, a batch per click so the request stays short.</summary>
    public async Task<IActionResult> OnPostBackfillPhotosAsync(CancellationToken ct)
    {
        var batch = await db.PropertyPhotos.Where(x => x.Variants == null).OrderBy(x => x.Id).Take(BackfillBatch).ToListAsync(ct);
        var missing = 0;
        foreach (var photo in batch)
            if (!await photos.BackfillAsync(photo, ct))
            {
                photo.Variants = ""; // original is gone; do not retry forever
                missing++;
            }
        await db.SaveChangesAsync(ct);
        Notice = $"برای {Fa.Digits(batch.Count.ToString())} عکس نسخهٔ سبک ساخته شد" + (missing > 0 ? $" ({Fa.Digits(missing.ToString())} فایل اصلی پیدا نشد)" : "") + ".";
        return RedirectToPage();
    }
}
