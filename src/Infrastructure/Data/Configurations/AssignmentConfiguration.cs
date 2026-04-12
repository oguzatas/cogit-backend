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

        // AccessKey is the magic-link credential — must be unique across all assignments.
        builder.Property(a => a.AccessKey)
            .HasMaxLength(64)
            .IsRequired();

        builder.HasIndex(a => a.AccessKey)
            .IsUnique();

        builder.HasOne(a => a.Tenant)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.Test)
            .WithMany(t => t.Assignments)
            .HasForeignKey(a => a.TestId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → TenantEmployee (the test-taker — no Identity account).
        builder.HasOne(a => a.TenantEmployee)
            .WithMany(e => e.Assignments)
            .HasForeignKey(a => a.TenantEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK → AppUser (the staff who created the assignment — nullable for bulk/system-generated).
        builder.HasOne(a => a.AssignedByStaff)
            .WithMany(u => u.AssignmentsAsStaff)
            .HasForeignKey(a => a.AssignedByStaffId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
