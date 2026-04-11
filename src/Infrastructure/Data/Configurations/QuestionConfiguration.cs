using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("Questions");

        builder.Property(q => q.Text)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(q => q.QuestionType)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(q => q.VariableKey)
            .HasMaxLength(100)
            .IsRequired();

        // Settings stored as JSONB — Npgsql handles serialization via System.Text.Json.
        builder.Property(q => q.Settings)
            .HasColumnType("jsonb");

        builder.HasOne(q => q.Test)
            .WithMany(t => t.Questions)
            .HasForeignKey(q => q.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global entity — no TenantId. Query filter (IsDeleted only) applied in ApplicationDbContext.
    }
}
