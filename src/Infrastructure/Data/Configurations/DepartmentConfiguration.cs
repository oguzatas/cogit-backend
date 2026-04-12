using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.ToTable("Departments");

        builder.Property(d => d.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Unique department name per tenant.
        builder.HasIndex(d => new { d.TenantId, d.Name })
            .IsUnique();

        builder.HasOne(d => d.Tenant)
            .WithMany(t => t.Departments)
            .HasForeignKey(d => d.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
