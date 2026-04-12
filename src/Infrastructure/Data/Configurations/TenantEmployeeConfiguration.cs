using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class TenantEmployeeConfiguration : IEntityTypeConfiguration<TenantEmployee>
{
    public void Configure(EntityTypeBuilder<TenantEmployee> builder)
    {
        builder.ToTable("TenantEmployees");

        builder.Property(e => e.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasMaxLength(320)
            .IsRequired();

        // Email uniqueness is scoped per-tenant (same person can be in multiple tenants).
        builder.HasIndex(e => new { e.TenantId, e.Email })
            .IsUnique();

        builder.HasOne(e => e.Tenant)
            .WithMany(t => t.TenantEmployees)
            .HasForeignKey(e => e.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Department)
            .WithMany(d => d.TenantEmployees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
