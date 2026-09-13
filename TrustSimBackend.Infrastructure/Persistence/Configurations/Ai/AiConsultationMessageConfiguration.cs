using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class AiConsultationMessageConfiguration : IEntityTypeConfiguration<AiConsultationMessage>
{
    public void Configure(EntityTypeBuilder<AiConsultationMessage> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SenderRole).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.MessageContent).IsRequired();
        builder.Property(x => x.RecommendedSimIds).HasColumnType("jsonb");
        builder.Property(x => x.ReferencedChunkIds).HasColumnType("jsonb");

        builder.HasOne(x => x.Session)
            .WithMany(s => s.Messages)
            .HasForeignKey(x => x.SessionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SessionId);
    }
}
