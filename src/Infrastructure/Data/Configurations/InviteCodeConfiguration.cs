using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class InviteCodeConfiguration : IEntityTypeConfiguration<InviteCode>
{
    public void Configure(EntityTypeBuilder<InviteCode> builder)
    {
        builder.ToTable("InviteCodes");

        builder.Property(c => c.Code)
            .HasMaxLength(64)
            .IsRequired();

        // Code must be globally unique — it IS the security credential.
        builder.HasIndex(c => c.Code)
            .IsUnique();

        // IsActive is a computed property — not mapped to a column.
        builder.Ignore(c => c.IsActive);

        builder.HasOne(c => c.Tenant)
            .WithMany(t => t.InviteCodes)
            .HasForeignKey(c => c.TenantId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(c => c.Department)
            .WithMany(d => d.InviteCodes)
            .HasForeignKey(c => c.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
        // NOTE: RedeemInviteCode handler uses IgnoreQueryFilters() because the
        // caller is anonymous and has no TenantId claim.
    }
}
