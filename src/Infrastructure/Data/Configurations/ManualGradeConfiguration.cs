using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class ManualGradeConfiguration : IEntityTypeConfiguration<ManualGrade>
{
    public void Configure(EntityTypeBuilder<ManualGrade> builder)
    {
        builder.ToTable("ManualGrades");

        builder.Property(g => g.Points).IsRequired();

        // One grade per variable per assignment — calling the endpoint again replaces the value.
        builder.HasIndex(g => new { g.AssignmentId, g.TestVariableId }).IsUnique();

        // Cascade: deleting an Assignment removes its manual grades.
        builder.HasOne(g => g.Assignment)
            .WithMany(a => a.ManualGrades)
            .HasForeignKey(g => g.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: preserve grade history even if the variable definition is soft-deleted.
        builder.HasOne(g => g.TestVariable)
            .WithMany(v => v.ManualGrades)
            .HasForeignKey(g => g.TestVariableId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
