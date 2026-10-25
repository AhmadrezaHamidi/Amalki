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
    public DbSet<SavedSearch> SavedSearches => Set<SavedSearch>();
    public DbSet<AlertSent> AlertsSent => Set<AlertSent>();
    public DbSet<SmsOptOut> SmsOptOuts => Set<SmsOptOut>();
    public DbSet<SmsLog> SmsLogs => Set<SmsLog>();
    public DbSet<SmsCampaign> SmsCampaigns => Set<SmsCampaign>();
    public DbSet<PhoneOtp> PhoneOtps => Set<PhoneOtp>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
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
        b.Entity<ChannelPost>().HasIndex(x => new { x.PropertyId, x.Channel }).IsUnique();

        b.Entity<SavedSearch>(e =>
        {
            e.Property(x => x.Phone).HasMaxLength(11);
            e.Property(x => x.Name).HasMaxLength(80);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.Districts).HasMaxLength(400);
            e.HasIndex(x => new { x.Active, x.Deal, x.City });
            e.HasIndex(x => x.Phone);
        });
        b.Entity<AlertSent>().HasIndex(x => new { x.SavedSearchId, x.PropertyId, x.Kind, x.Price }).IsUnique();
        b.Entity<SmsOptOut>(e => { e.HasKey(x => x.Phone); e.Property(x => x.Phone).HasMaxLength(11); });
        b.Entity<SmsLog>(e =>
        {
            e.Property(x => x.Phone).HasMaxLength(11);
            e.Property(x => x.Text).HasMaxLength(1000);
            e.Property(x => x.Error).HasMaxLength(500);
            e.Property(x => x.ProviderMessageId).HasMaxLength(100);
            e.HasIndex(x => x.CampaignId);
        });
        b.Entity<SmsCampaign>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(150);
            e.Property(x => x.Template).HasMaxLength(1000);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.District).HasMaxLength(80);
            e.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        });
        b.Entity<PhoneOtp>(e =>
        {
            e.Property(x => x.Phone).HasMaxLength(11);
            e.Property(x => x.CodeHash).HasMaxLength(64);
            e.HasIndex(x => new { x.Phone, x.CreatedAtUtc });
        });
    }
}
