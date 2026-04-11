using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class ScoringScaleConfiguration : IEntityTypeConfiguration<ScoringScale>
{
    public void Configure(EntityTypeBuilder<ScoringScale> builder)
    {
        builder.ToTable("ScoringScales");

        builder.Property(s => s.Name)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(s => s.FormulaExpression)
            .HasMaxLength(2000)
            .IsRequired();

        builder.HasOne(s => s.Test)
            .WithMany(t => t.ScoringScales)
            .HasForeignKey(s => s.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global entity — no TenantId. Query filter (IsDeleted only) applied in ApplicationDbContext.
    }
}
