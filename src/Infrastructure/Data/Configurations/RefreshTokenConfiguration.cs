using backend.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.Token)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(r => r.Token)
            .IsUnique();

        builder.HasIndex(r => r.IdentityUserId);

        builder.HasOne(r => r.IdentityUser)
            .WithMany()
            .HasForeignKey(r => r.IdentityUserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
