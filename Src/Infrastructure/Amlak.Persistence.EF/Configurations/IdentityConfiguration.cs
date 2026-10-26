using Amlak.Domain.Identity.Aggregates;
using Amlak.Domain.Identity.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Amlak.Persistence.EF.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(11);
        builder.Property(x => x.FirstName).HasMaxLength(50);
        builder.Property(x => x.LastName).HasMaxLength(50);
        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Ignore(x => x.DisplayName);
        builder.HasIndex(x => x.PhoneNumber).IsUnique();
        builder.HasQueryFilter(x => !x.IsDeleted);
    }
}

public sealed class OtpConfiguration : IEntityTypeConfiguration<OtpRequest>
{
    public void Configure(EntityTypeBuilder<OtpRequest> builder)
    {
        builder.ToTable("OtpRequests");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(11);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(6);
        builder.Ignore(x => x.IsExpired);
        builder.Ignore(x => x.IsValid);
        builder.HasIndex(x => new { x.PhoneNumber, x.CreatedAt });
    }
}

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.Token).IsRequired().HasMaxLength(200);
        builder.HasIndex(x => x.Token).IsUnique();
        builder.HasIndex(x => x.UserId);
    }
}
