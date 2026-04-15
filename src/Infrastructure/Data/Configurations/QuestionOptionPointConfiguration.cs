using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class QuestionOptionPointConfiguration : IEntityTypeConfiguration<QuestionOptionPoint>
{
    public void Configure(EntityTypeBuilder<QuestionOptionPoint> builder)
    {
        builder.ToTable("QuestionOptionPoints");

        builder.Property(p => p.Points)
            .IsRequired();

        // Composite unique index: one variable can receive at most one point entry
        // per option — no accidental duplicate accumulation.
        builder.HasIndex(p => new { p.OptionId, p.TestVariableId })
            .IsUnique();

        // Cascade: deleting a QuestionOption removes its point mappings.
        builder.HasOne(p => p.Option)
            .WithMany(o => o.QuestionOptionPoints)
            .HasForeignKey(p => p.OptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Cascade: deleting a TestVariable removes all point mappings referencing it.
        builder.HasOne(p => p.TestVariable)
            .WithMany(v => v.QuestionOptionPoints)
            .HasForeignKey(p => p.TestVariableId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global entity — no TenantId. Query filter (IsDeleted only) applied in ApplicationDbContext.
    }
}
