using Amlak.Web.Domain;
using Amlak.Web.Read;
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

    // Read side (CQRS): keyless, mapped to SQL views, never written through EF.
    public DbSet<ListingCard> ListingCards => Set<ListingCard>();
    public DbSet<ListingFacet> ListingFacets => Set<ListingFacet>();
    public DbSet<AdminCounters> AdminCounters => Set<AdminCounters>();

    public const string SortPrice = "SortPrice";

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);
        b.Entity<Property>(e =>
        {
            e.HasIndex(x => x.Slug).IsUnique();
            // Landing pages filter by deal/city/kind and show newest first: seek and read in order.
            e.HasIndex(x => new { x.Status, x.Deal, x.City, x.Kind, x.PublishedAtUtc });
            e.HasIndex(x => new { x.Status, x.City, x.District });
            e.HasIndex(x => new { x.Status, x.PublishedAtUtc });
            // What visitors filter and sort by: price for sale, deposit for rent. Stored so it can be indexed.
            // ISNULL on SQL Server so the column is NOT NULL, which the indexed facet view needs to SUM it.
            e.Property<long>(SortPrice).HasComputedColumnSql(
                Database.IsSqlServer() ? "ISNULL([Price], ISNULL([Deposit], CAST(0 AS bigint)))" : "COALESCE([Price], [Deposit], 0)",
                stored: true);
            e.HasIndex(nameof(Property.Status), SortPrice);
            e.Property(x => x.Slug).HasMaxLength(200);
            e.Property(x => x.Title).HasMaxLength(200);
            e.Property(x => x.City).HasMaxLength(80);
            e.Property(x => x.District).HasMaxLength(80);
            e.HasMany(x => x.Photos).WithOne().HasForeignKey(p => p.PropertyId).OnDelete(DeleteBehavior.Cascade);
            e.HasMany(x => x.ChannelPosts).WithOne().HasForeignKey(p => p.PropertyId).OnDelete(DeleteBehavior.Cascade);
        });
        b.Entity<PropertyPhoto>(e =>
        {
            e.Property(x => x.Variants).HasMaxLength(40);
            // Cover photo lookup (first by SortOrder) for the listing card view, covering what the card renders.
            e.HasIndex(x => new { x.PropertyId, x.SortOrder }).IncludeProperties(x => new { x.Url, x.Alt, x.Width, x.Height, x.Variants });
        });
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

        b.Entity<ListingCard>(e => { e.HasNoKey(); e.ToView("vw_ListingCards"); });
        b.Entity<AdminCounters>(e => { e.HasNoKey(); e.ToView("vw_AdminCounters"); });
        b.Entity<ListingFacet>(e =>
        {
            e.HasNoKey();
            // Indexed view on SQL Server: NOEXPAND makes every edition read the stored rows instead of re-aggregating.
            if (Database.IsSqlServer())
                e.ToTable((string?)null).ToSqlQuery("SELECT Deal, Kind, City, District, Listings, SumPrice, SumArea FROM dbo.vw_ListingFacets WITH (NOEXPAND)");
            else
                e.ToView("vw_ListingFacets");
        });
    }
}
