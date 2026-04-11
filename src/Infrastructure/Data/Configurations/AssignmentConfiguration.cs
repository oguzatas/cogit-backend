using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class AssignmentConfiguration : IEntityTypeConfiguration<Assignment>
{
    public void Configure(EntityTypeBuilder<Assignment> builder)
    {
        builder.ToTable("Assignments");

        builder.Property(a => a.Status)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Test)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TestId)
            .OnDelete(DeleteBehavior.Restrict);

        // CRITICAL: Restrict both user FKs — no cascade to avoid multi-path delete conflicts.
        builder.HasOne(a => a.Client)
            .WithMany(u => u.AssignmentsAsClient)
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.AssignedByStaff)
            .WithMany(u => u.AssignmentsAsStaff)
            .HasForeignKey(a => a.AssignedByStaffId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
