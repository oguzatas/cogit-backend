using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class AssignmentResultConfiguration : IEntityTypeConfiguration<AssignmentResult>
{
    public void Configure(EntityTypeBuilder<AssignmentResult> builder)
    {
        builder.ToTable("AssignmentResults");

        // Nullable — mutually exclusive with ResultText.
        // Numeric results (double/int/decimal) are stored here.
        builder.Property(r => r.CalculatedScore)
            .HasPrecision(18, 4)
            .IsRequired(false);

        // String results (e.g. "ENTJ", "High Risk") are stored here.
        builder.Property(r => r.ResultText)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.HasOne(r => r.Assignment)
            .WithMany(a => a.AssignmentResults)
            .HasForeignKey(r => r.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(r => r.Scale)
            .WithMany(s => s.AssignmentResults)
            .HasForeignKey(r => r.ScaleId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
