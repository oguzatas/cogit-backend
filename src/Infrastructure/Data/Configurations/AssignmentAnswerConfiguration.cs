using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class AssignmentAnswerConfiguration : IEntityTypeConfiguration<AssignmentAnswer>
{
    public void Configure(EntityTypeBuilder<AssignmentAnswer> builder)
    {
        builder.ToTable("AssignmentAnswers");

        // ── SelectedOptionIds — EF Core 8 primitive collection → JSONB ────────
        // Stored as a native PostgreSQL JSONB array (e.g. [1, 5, 9]).
        // No FK to QuestionOption; referential integrity is enforced at the
        // application layer when the scoring engine resolves option IDs.
        builder.Property(a => a.SelectedOptionIds)
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(a => a.TextValue)
            .HasMaxLength(4000);

        // ── Foreign keys ──────────────────────────────────────────────────────

        // Cascade: deleting an Assignment wipes all its answers.
        builder.HasOne(a => a.Assignment)
            .WithMany(x => x.AssignmentAnswers)
            .HasForeignKey(a => a.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        // Restrict: a Question cannot be deleted while answers reference it.
        builder.HasOne(a => a.Question)
            .WithMany(q => q.AssignmentAnswers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
