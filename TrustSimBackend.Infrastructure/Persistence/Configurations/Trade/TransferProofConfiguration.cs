using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TrustSimBackend.Domain.Entities.Trade;

namespace TrustSimBackend.Infrastructure.Persistence.Configurations.Trade;

public class TransferProofConfiguration : IEntityTypeConfiguration<TransferProof>
{
    public void Configure(EntityTypeBuilder<TransferProof> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImageUrl).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(500);

        builder.Property(x => x.ProofStatus)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.Property(x => x.ProofType)
               .IsRequired()
               .HasConversion<string>()
               .HasMaxLength(50);

        builder.HasOne(x => x.SimTransfer)
               .WithMany(s => s.TransferProofs)
               .HasForeignKey(x => x.SimTransferId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SimTransferId);
    }
}
