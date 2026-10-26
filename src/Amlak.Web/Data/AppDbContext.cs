using Amlak.Web.Domain;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Amlak.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : IdentityDbContext<IdentityUser>(options)
{
    public DbSet<Property> Properties => Set<Property>();
    public DbSet<PropertyPhoto> PropertyPhotos => Set<PropertyPhoto>();
    public DbSet<ChannelPost> ChannelPosts => Set<ChannelPost>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        SaveWithEventsAsync(acceptAllChangesOnSuccess, sync: true, default).GetAwaiter().GetResult();

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken ct = default) =>
        SaveWithEventsAsync(acceptAllChangesOnSuccess, sync: false, ct);

    /// <summary>
    /// Saves the changes and, in the same transaction, one outbox row per domain event the aggregates raised.
    /// The rows are written after the changes so that new aggregates already have their database ids.
    /// </summary>
    private async Task<int> SaveWithEventsAsync(bool accept, bool sync, CancellationToken ct)
    {
        var sources = ChangeTracker.Entries<AggregateRoot>().Select(e => e.Entity).Where(a => a.DomainEvents.Count > 0).ToList();
        if (sources.Count == 0) return sync ? base.SaveChanges(accept) : await base.SaveChangesAsync(accept, ct);

        var events = sources.SelectMany(a => a.DomainEvents).ToList();
        var own = Database.CurrentTransaction is null
            ? sync ? Database.BeginTransaction() : await Database.BeginTransactionAsync(ct)
            : null;
        try
        {
            var n = sync ? base.SaveChanges(accept) : await base.SaveChangesAsync(accept, ct);
            OutboxMessages.AddRange(events.Select(OutboxMessage.From));
            n += sync ? base.SaveChanges(accept) : await base.SaveChangesAsync(accept, ct);
            if (own is not null)
            {
                if (sync) own.Commit(); else await own.CommitAsync(ct);
            }
            foreach (var a in sources) a.ClearDomainEvents();
            return n;
        }
        finally
        {
            if (own is not null) await own.DisposeAsync();
        }
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Property>().Ignore(x => x.DomainEvents);
        b.Entity<Lead>().Ignore(x => x.DomainEvents);
        b.Entity<Ticket>().Ignore(x => x.DomainEvents);
        b.Entity<Property>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            e.HasIndex(x => new { x.Status, x.Deal, x.City });
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.District).HasMaxLength(80);
            e.HasMany(x => x.Photos).WithOne().HasForeignKey(p => p.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ChannelPosts).WithOne().HasForeignKey(p => p.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PropertyPhoto>().Property(x => x.Variants).HasMaxLength(40);
        b.Entity<ChannelPost>(e =>
        {
            e.HasIndex(x => new { x.PropertyId, x.Channel }).IsUnique();
            e.Property(x => x.ExtraMessageIds).HasMaxLength(200);
            e.Property(x => x.PhotoKey).HasMaxLength(64);
        });
        b.Entity<Lead>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Message).HasMaxLength(1000);
            e.HasIndex(x => new { x.Handled, x.CreatedAtUtc });
            // Keep the lead when its listing is deleted.
            e.HasOne(x => x.Property).WithMany().HasForeignKey(x => x.PropertyId).OnDelete(DeleteBehavior.SetNull);
        });
        b.Entity<Ticket>(e =>
        {
            e.HasIndex(x => x.Code).IsUnique();
            e.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
            e.Property(x => x.Code).HasMaxLength(16);
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.Phone).HasMaxLength(20);
            e.Property(x => x.Subject).HasMaxLength(150);
            e.HasMany(x => x.Messages).WithOne().HasForeignKey(m => m.TicketId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<TicketMessage>().Property(x => x.Body).HasMaxLength(4000);
        b.Entity<OutboxMessage>(e =>
        {
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.Error).HasMaxLength(500);
            // The dispatcher's query: unprocessed rows that are due, oldest first.
            e.HasIndex(x => new { x.ProcessedAtUtc, x.NextAttemptAtUtc });
        });
    }
}
