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
    }
}
