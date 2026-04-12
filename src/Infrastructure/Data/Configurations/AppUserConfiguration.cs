using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class AppUserConfiguration : IEntityTypeConfiguration<AppUser>
{
    public void Configure(EntityTypeBuilder<AppUser> builder)
    {
        builder.ToTable("AppUsers");

        builder.Property(u => u.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        // TenantId nullable — null means SuperAdmin (no tenant scope).
        builder.HasOne(u => u.Tenant)
            .WithMany(t => t.AppUsers)
            .HasForeignKey(u => u.TenantId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + optional TenantId scope) applied in ApplicationDbContext.
    }
}
