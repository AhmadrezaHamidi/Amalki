using Amlak.Web.Data;
using Amlak.Web.Services.Publishing;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Pages.Admin;

/// <summary>Dashboard: what is waiting for the admin today.</summary>
public class IndexModel(AppDbContext db, ChannelPublisher publisher) : PageModel
{
    public int Published { get; private set; }
    public int Drafts { get; private set; }
    public int NewLeads { get; private set; }
    public int OpenTickets { get; private set; }
    public int AnsweredTickets { get; private set; }
    public int ChannelErrors { get; private set; }
    public List<Ticket> Tickets { get; private set; } = [];
    public List<Lead> Leads { get; private set; } = [];
    public IReadOnlyCollection<Channel> Channels => publisher.ConfiguredChannels;

    public async Task OnGetAsync()
    {
        Published = await db.Properties.CountAsync(p => p.Status == ListingStatus.Published);
        Drafts = await db.Properties.CountAsync(p => p.Status == ListingStatus.Draft);
        NewLeads = await db.Leads.CountAsync(l => !l.Handled);
        OpenTickets = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Open);
        AnsweredTickets = await db.Tickets.CountAsync(t => t.Status == TicketStatus.Answered);
        ChannelErrors = await db.ChannelPosts.CountAsync(c => c.Error != null);
        Tickets = await db.Tickets.AsNoTracking().Where(t => t.Status != TicketStatus.Closed)
            .OrderBy(t => t.Status).ThenByDescending(t => t.UpdatedAtUtc).Take(6).ToListAsync();
        Leads = await db.Leads.AsNoTracking().Include(l => l.Property).Where(l => !l.Handled)
            .OrderByDescending(l => l.CreatedAtUtc).Take(6).ToListAsync();
    }
}
