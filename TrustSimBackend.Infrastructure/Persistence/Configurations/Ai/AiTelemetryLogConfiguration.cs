using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class AiTelemetryLogConfiguration : IEntityTypeConfiguration<AiTelemetryLog>
{
    public void Configure(EntityTypeBuilder<AiTelemetryLog> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationType).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Intent).HasMaxLength(60).IsRequired();
        builder.Property(x => x.ModelUsed).HasMaxLength(100);
        builder.Property(x => x.EngineSource).HasMaxLength(60).IsRequired();
        builder.Property(x => x.CacheLayer).HasMaxLength(30).IsRequired();
        builder.Property(x => x.ToolsInvokedJson).HasColumnType("jsonb");
        builder.Property(x => x.ReferencedChunkIdsJson).HasColumnType("jsonb");
        builder.Property(x => x.GuardrailReason).HasMaxLength(300);
        builder.Property(x => x.DeniedToolName).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);
        builder.Property(x => x.UserPromptPreview).HasMaxLength(300);

        builder.HasIndex(x => x.CreatedDate);
        builder.HasIndex(x => x.SessionId);
        builder.HasIndex(x => x.UserAccountId);
        builder.HasIndex(x => new { x.OperationType, x.Intent });
    }
}
