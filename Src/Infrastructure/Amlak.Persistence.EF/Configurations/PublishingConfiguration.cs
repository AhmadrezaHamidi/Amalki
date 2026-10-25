using Amlak.Domain.Publishing.Aggregates;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amlak.Persistence.EF.Configurations;

public sealed class ChannelPostConfiguration : IEntityTypeConfiguration<ChannelPost>
{
    public void Configure(EntityTypeBuilder<ChannelPost> builder)
    {
        builder.ToTable("ChannelPosts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.ExternalMessageId).HasMaxLength(50);
        builder.Property(x => x.Error).HasMaxLength(ChannelPost.MaxErrorLength);
        builder.Ignore(x => x.IsSent);

        // یک پست برای هر (آگهی، کانال) — جلوی پست تکراری را می‌گیرد
        builder.HasIndex(x => new { x.PropertyId, x.Channel }).IsUnique();
    }
}
