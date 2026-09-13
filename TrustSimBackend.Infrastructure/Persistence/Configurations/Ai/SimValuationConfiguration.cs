using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Ai;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Ai;

public class SimValuationConfiguration : IEntityTypeConfiguration<SimValuation>
{
    public void Configure(EntityTypeBuilder<SimValuation> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.SuggestedPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MinPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.MaxPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.BaselinePrice).HasPrecision(18, 2).IsRequired();
        builder.Property(x => x.ValuationReasoning).IsRequired();
        builder.Property(x => x.AiModelName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ReferencedChunkIds).HasColumnType("jsonb");

        // Quan hệ với SimNumber
        builder.HasOne(x => x.SimNumber)
            .WithMany(s => s.SimValuations)
            .HasForeignKey(x => x.SimNumberId)
            .OnDelete(DeleteBehavior.Cascade);

        // Quan hệ với UserAccount (nếu có)
        builder.HasOne(x => x.UserAccount)
            .WithMany()
            .HasForeignKey(x => x.UserAccountId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SimNumberId);
        builder.HasIndex(x => x.UserAccountId);
    }
}
