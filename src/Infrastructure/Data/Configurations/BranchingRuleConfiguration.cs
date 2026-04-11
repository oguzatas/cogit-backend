using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class BranchingRuleConfiguration : IEntityTypeConfiguration<BranchingRule>
{
    public void Configure(EntityTypeBuilder<BranchingRule> builder)
    {
        builder.ToTable("BranchingRules");

        builder.Property(r => r.ConditionExpression)
            .HasMaxLength(1000)
            .IsRequired();

        // Restrict both FKs to prevent accidental cascading deletes across question graph.
        builder.HasOne(r => r.SourceQuestion)
            .WithMany(q => q.BranchingRulesAsSource)
            .HasForeignKey(r => r.SourceQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(r => r.TargetQuestion)
            .WithMany(q => q.BranchingRulesAsTarget)
            .HasForeignKey(r => r.TargetQuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Global entity — no TenantId. Query filter (IsDeleted only) applied in ApplicationDbContext.
    }
}
