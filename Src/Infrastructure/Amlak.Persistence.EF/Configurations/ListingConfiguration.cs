using Amlak.Domain.Listing.Aggregates;
using Amlak.Domain.Listing.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amlak.Persistence.EF.Configurations;

public sealed class PropertyConfiguration : IEntityTypeConfiguration<Property>
{
    public void Configure(EntityTypeBuilder<Property> builder)
    {
        builder.ToTable("Properties");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Slug).IsRequired().HasMaxLength(Amlak.Domain.Listing.Slug.MaxLength + 10);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(4000);
        builder.Property(x => x.Deal).HasConversion<string>().HasMaxLength(10).IsRequired();
        builder.Property(x => x.Kind).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.City).IsRequired().HasMaxLength(50);
        builder.Property(x => x.District).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Address).HasMaxLength(300);

        builder.Ignore(x => x.IsPublished);

        // slug آگهی حذف‌شده هم رزرو می‌ماند تا لینک قدیمی به آگهی دیگری نرسد
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.HasIndex(x => new { x.Status, x.Deal, x.City });

        builder.HasMany(x => x.Photos)
               .WithOne()
               .HasForeignKey(p => p.PropertyId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Photos).HasField("_photos").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class PropertyPhotoConfiguration : IEntityTypeConfiguration<PropertyPhoto>
{
    public void Configure(EntityTypeBuilder<PropertyPhoto> builder)
    {
        builder.ToTable("PropertyPhotos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FileName).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Alt).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => new { x.PropertyId, x.SortOrder });
    }
}
