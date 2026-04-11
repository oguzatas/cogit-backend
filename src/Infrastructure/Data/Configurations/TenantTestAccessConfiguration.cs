using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class TenantTestAccessConfiguration : IEntityTypeConfiguration<TenantTestAccess>
{
    public void Configure(EntityTypeBuilder<TenantTestAccess> builder)
    {
        builder.ToTable("TenantTestAccesses");

        // Enforce one access grant per tenant-test pair.
        builder.HasIndex(a => new { a.TenantId, a.TestId })
            .IsUnique();

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.TenantTestAccesses)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Test)
            .WithMany(t => t.TenantTestAccesses)
            .HasForeignKey(a => a.TestId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
