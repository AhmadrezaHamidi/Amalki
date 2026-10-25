using Amlak.Domain.Lead.Aggregates;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amlak.Persistence.EF.Configurations;

public sealed class ContactLeadConfiguration : IEntityTypeConfiguration<ContactLead>
{
    public void Configure(EntityTypeBuilder<ContactLead> builder)
    {
        builder.ToTable("Leads");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(11);
        builder.Property(x => x.Message).HasMaxLength(1000);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.AdminNote).HasMaxLength(1000);

        builder.HasIndex(x => new { x.PhoneNumber, x.CreatedAt });
        builder.HasIndex(x => new { x.Status, x.CreatedAt });
        builder.HasIndex(x => x.PropertyId);
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}
