using Amlak.Domain.Identity.Aggregates;
using Amlak.Domain.Lead.Aggregates;
using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Entities;
using Amlak.Domain.Publishing.Aggregates;
using RefreshToken = Amlak.Domain.Identity.Entities.RefreshToken;

namespace Amlak.Persistence.EF;

/// <summary>DbContext اصلی سامانهٔ املاک</summary>
public sealed class AmlakDbContext(DbContextOptions<AmlakDbContext> options) : DbContext(options)
{
    // ── Identity ──────────────────────────────────────────────────────────
    public DbSet<User>         Users         { get; set; }
    public DbSet<OtpRequest>   OtpRequests   { get; set; }
    public DbSet<RefreshToken> RefreshTokens { get; set; }

    // ── Listing (آگهی) ────────────────────────────────────────────────────
    public DbSet<Property>      Properties     { get; set; }
    public DbSet<PropertyPhoto> PropertyPhotos { get; set; }

    // ── Publishing (کانال‌ها) ─────────────────────────────────────────────
    public DbSet<ChannelPost> ChannelPosts { get; set; }

    // ── Lead (درخواست تماس) ──────────────────────────────────────────────
    public DbSet<ContactLead> Leads { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AmlakDbContext).Assembly);
    }

    /// <summary>
    /// برداشتن رویدادهای دامین از aggregate های ردیابی‌شده و پاک کردن آن‌ها.
    /// پس از اتمام Command صدا زده می‌شود تا رویدادها فقط روی تغییرات ذخیره‌شده منتشر شوند.
    /// </summary>
    public IReadOnlyList<AhmadBase.Doamin.IEvent> DrainDomainEvents()
    {
        var aggregates = ChangeTracker
            .Entries<AhmadBase.Doamin.IAggregateRoot>()
            .Select(e => e.Entity)
            .Where(a => a.DomainEvents.Count > 0)
            .ToList();

        if (aggregates.Count == 0) return [];

        var events = aggregates.SelectMany(a => a.DomainEvents).ToList();
        aggregates.ForEach(a => a.ClearDomainEvents());
        return events;
    }
}
