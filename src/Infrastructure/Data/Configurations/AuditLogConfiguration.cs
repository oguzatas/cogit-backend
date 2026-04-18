using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.HasKey(a => a.Id);

        builder.Property(a => a.UserId)
            .HasMaxLength(450);          // matches ASP.NET Identity PK length

        builder.Property(a => a.ActionName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        // Timestamp stored with timezone (Npgsql timestamptz).
        builder.Property(a => a.Timestamp)
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────────

        // Dashboard & report queries filter by TenantId first.
        builder.HasIndex(a => a.TenantId);

        // Admin queries filter by actor.
        builder.HasIndex(a => a.UserId);

        // Table name
        builder.ToTable("AuditLogs");
    }
}
