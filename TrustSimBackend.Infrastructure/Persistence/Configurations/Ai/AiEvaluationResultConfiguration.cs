using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class AiEvaluationResultConfiguration : IEntityTypeConfiguration<AiEvaluationResult>
{
    public void Configure(EntityTypeBuilder<AiEvaluationResult> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.EvalType).HasMaxLength(40).IsRequired();
        builder.Property(x => x.TestCaseCode).HasMaxLength(80);
        builder.Property(x => x.HallucinationFlagsJson).HasColumnType("jsonb");
        builder.Property(x => x.EvaluationNotes).HasMaxLength(1000);

        builder.HasOne(x => x.TelemetryLog)
            .WithMany(t => t.EvaluationResults)
            .HasForeignKey(x => x.TelemetryLogId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.TelemetryLogId);
        builder.HasIndex(x => new { x.EvalType, x.Passed });
        builder.HasIndex(x => x.CreatedDate);
    }
}
