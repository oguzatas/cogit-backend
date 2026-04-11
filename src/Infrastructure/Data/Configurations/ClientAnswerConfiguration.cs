using backend.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace backend.Infrastructure.Data.Configurations;

public class ClientAnswerConfiguration : IEntityTypeConfiguration<ClientAnswer>
{
    public void Configure(EntityTypeBuilder<ClientAnswer> builder)
    {
        builder.ToTable("ClientAnswers");

        builder.Property(a => a.TextResponse)
            .HasMaxLength(4000);

        builder.HasOne(a => a.Assignment)
            .WithMany(x => x.ClientAnswers)
            .HasForeignKey(a => a.AssignmentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(a => a.Question)
            .WithMany(q => q.ClientAnswers)
            .HasForeignKey(a => a.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // SelectedOptionId is nullable — no answer stored for TextInput questions.
        builder.HasOne(a => a.SelectedOption)
            .WithMany(o => o.ClientAnswers)
            .HasForeignKey(a => a.SelectedOptionId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);

        // Query filter (IsDeleted + TenantId scope) applied in ApplicationDbContext.
    }
}
