using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class TestVariableConfiguration : IEntityTypeConfiguration<TestVariable>
{
    public void Configure(EntityTypeBuilder<TestVariable> builder)
    {
        builder.ToTable("TestVariables");

        builder.Property(v => v.Name)
            .HasMaxLength(300)
            .IsRequired();

        // Key is the short NCalc alias — kept deliberately short to stay readable
        // in formula expressions.
        builder.Property(v => v.Key)
            .HasMaxLength(100)
            .IsRequired();

        // Unique per test: two variables on the same test cannot share an alias.
        builder.HasIndex(v => new { v.TestId, v.Key })
            .IsUnique();

        builder.Property(v => v.DefaultValue)
            .HasDefaultValue(0.0);

        // Cascade: removing a Test wipes its variable definitions.
        builder.HasOne(v => v.Test)
            .WithMany(t => t.TestVariables)
            .HasForeignKey(v => v.TestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Global entity — no TenantId. Query filter (IsDeleted only) applied in ApplicationDbContext.
    }
}
